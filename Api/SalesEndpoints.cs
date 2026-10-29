using System.Globalization;
using StudioCore.Models;
using StudioCore.Services;

namespace StudioCore.Api;

/// <summary>
/// Đơn hàng, thanh toán, hoá đơn, hợp đồng và báo cáo doanh thu.
/// Payment là nguồn sự thật duy nhất cho số tiền đã thu của một đơn hàng.
/// </summary>
public static class SalesEndpoints
{
    private const string SalesModule = "orders";
    private const string ContractModule = "contracts";

    private static readonly string[] SchedulePresets =
        ["50/50", "30/40/30", "40/40/20", "30/70"];

    public static void Map(RouteGroupBuilder group)
    {
        var orders = new StudioPermissionFilter(StudioPermissions.Orders);
        var ordersRead = new StudioAnyPermissionFilter(
            StudioPermissions.Orders,
            StudioPermissions.Reports);
        var contracts = new StudioPermissionFilter(StudioPermissions.Contracts);
        var contractsRead = new StudioAnyPermissionFilter(
            StudioPermissions.Contracts,
            StudioPermissions.Reports);
        var reports = new StudioPermissionFilter(StudioPermissions.Reports);
        var reportsRead = new StudioAnyPermissionFilter(
            StudioPermissions.Reports,
            StudioPermissions.Orders);

        group.MapGet("/orders", GetOrders).AddEndpointFilter(ordersRead);
        group.MapGet("/orders/{id}", GetOrder).AddEndpointFilter(ordersRead);
        group.MapGet("/orders/{id}/invoice", GetInvoice).AddEndpointFilter(ordersRead);
        group.MapPost("/orders", CreateOrder).AddEndpointFilter(orders);
        group.MapPut("/orders/{id}", UpdateOrder).AddEndpointFilter(orders);
        group.MapPut("/orders/{id}/status", UpdateOrderStatus).AddEndpointFilter(orders);
        group.MapDelete("/orders/{id}", DeleteOrder).AddEndpointFilter(orders);
        group.MapPost("/orders/{id}/payments", RecordPayment).AddEndpointFilter(orders);
        group.MapDelete("/orders/{id}/payments/{paymentId}", DeletePayment).AddEndpointFilter(orders);

        group.MapGet("/contracts", GetContracts).AddEndpointFilter(contractsRead);
        group.MapGet("/contracts/export", ExportContracts).AddEndpointFilter(contractsRead);
        group.MapGet("/contracts/{id}", GetContract).AddEndpointFilter(contractsRead);
        group.MapPost("/contracts", CreateContract).AddEndpointFilter(contracts);
        group.MapPut("/contracts/{id}", UpdateContract).AddEndpointFilter(contracts);
        group.MapPut("/contracts/{id}/schedule/{index:int}", ToggleSchedule).AddEndpointFilter(contracts);
        group.MapDelete("/contracts/{id}", DeleteContract).AddEndpointFilter(contracts);

        group.MapGet("/reports/summary", GetReportSummary).AddEndpointFilter(reports);
        group.MapGet("/reports/overview", GetReportOverview).AddEndpointFilter(reportsRead);
    }

    // ---------------------------------------------------------------- orders

    private static IResult GetOrders(DataStore store)
    {
        var payload = store.Read(data => data.Orders
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Code, StringComparer.OrdinalIgnoreCase)
            .Select(order => ApiMappings.ToOrder(order, data))
            .ToList());
        return Results.Ok(payload);
    }

    private static IResult GetOrder(string id, DataStore store)
    {
        var payload = store.Read(data =>
        {
            var order = FindOrder(data, id);
            var payments = data.Payments
                .Where(payment => string.Equals(payment.OrderId, order.Id, StringComparison.Ordinal))
                .OrderByDescending(payment => payment.PaidAt, StringComparer.Ordinal)
                .ThenByDescending(payment => payment.CreatedAt)
                .ToList();
            return new OrderDetailDto(
                ApiMappings.ToOrder(order, data, payments),
                payments.Select(payment => ApiMappings.ToPayment(payment, data)).ToList());
        });
        return Results.Ok(payload);
    }

    private static IResult CreateOrder(
        CreateOrderRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var items = NormalizeItemsOrThrow(request.Items);
        var status = NormalizeOrderStatusOrThrow(request.Status);
        var now = DateTimeOffset.UtcNow;

        var dto = store.Write(data =>
        {
            var customer = ResolveCustomer(data, request.CustomerId, required: true)!;
            var booking = ResolveBooking(data, request.BookingId);
            var sequence = data.OrderSequence;
            var order = new Order
            {
                Id = NewShortId(),
                Code = NextCode("DH-", data.Orders.Select(item => item.Code), ref sequence),
                CustomerId = customer.Id,
                BookingId = booking?.Id ?? "",
                Items = items,
                Discount = Math.Clamp(request.Discount, 0, 1_000_000_000m),
                Status = status,
                Notes = AlbumText.TrimTo(request.Notes, 2000),
                CreatedAt = now,
                UpdatedAt = now
            };
            data.Orders.Add(order);
            data.OrderSequence = sequence;
            StudioAudit.Add(
                data,
                actor,
                SalesModule,
                "create",
                $"đã tạo đơn hàng {order.Code} cho “{customer.Name}”",
                order.Id,
                [
                    $"Tổng tiền: {FormatMoney(order.Items.Sum(item => item.Quantity * item.UnitPrice) - order.Discount)}",
                    booking is null ? "" : $"Lịch chụp: {booking.Code}"
                ]);
            return ApiMappings.ToOrder(order, data);
        });

        return Results.Created($"/api/admin/orders/{dto.Id}", dto);
    }

    private static IResult UpdateOrder(
        string id,
        UpdateOrderRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var items = request.Items is null ? null : NormalizeItemsOrThrow(request.Items);
        var status = request.Status is null ? null : NormalizeOrderStatusOrThrow(request.Status);

        var dto = store.Write(data =>
        {
            var order = FindOrder(data, id);
            var changes = new List<string>();

            if (request.CustomerId is not null)
            {
                var customer = ResolveCustomer(data, request.CustomerId, required: true)!;
                if (!string.Equals(order.CustomerId, customer.Id, StringComparison.Ordinal))
                {
                    changes.Add($"Khách hàng: {CustomerName(data, order.CustomerId)} → {customer.Name}");
                }

                order.CustomerId = customer.Id;
                foreach (var payment in data.Payments.Where(payment =>
                             string.Equals(payment.OrderId, order.Id, StringComparison.Ordinal)))
                {
                    payment.CustomerId = customer.Id;
                }
            }

            if (request.BookingId is not null)
            {
                var booking = ResolveBooking(data, request.BookingId);
                order.BookingId = booking?.Id ?? "";
            }

            if (items is not null)
            {
                order.Items = items;
                changes.Add($"Danh sách hạng mục: {items.Count} dòng");
            }

            if (request.Discount is not null)
            {
                order.Discount = Math.Clamp(request.Discount.Value, 0, 1_000_000_000m);
            }

            if (status is not null && !string.Equals(order.Status, status, StringComparison.Ordinal))
            {
                changes.Add($"Trạng thái: {OrderStatusLabel(order.Status)} → {OrderStatusLabel(status)}");
                order.Status = status;
            }

            if (request.Notes is not null)
            {
                order.Notes = AlbumText.TrimTo(request.Notes, 2000);
            }

            order.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                SalesModule,
                "update",
                $"đã cập nhật đơn hàng {order.Code}",
                order.Id,
                changes);
            return ApiMappings.ToOrder(order, data);
        });

        return Results.Ok(dto);
    }

    private static IResult UpdateOrderStatus(
        string id,
        UpdateOrderStatusRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var status = NormalizeOrderStatusOrThrow(request.Status);

        var dto = store.Write(data =>
        {
            var order = FindOrder(data, id);
            var previous = order.Status;
            order.Status = status;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                SalesModule,
                "status",
                $"đã đổi trạng thái đơn {order.Code} sang “{OrderStatusLabel(status)}”",
                order.Id,
                [$"Trước đó: {OrderStatusLabel(previous)}"]);
            return ApiMappings.ToOrder(order, data);
        });

        return Results.Ok(dto);
    }

    private static IResult DeleteOrder(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        store.Write(data =>
        {
            var order = FindOrder(data, id);
            var paymentCount = data.Payments.Count(payment =>
                string.Equals(payment.OrderId, order.Id, StringComparison.Ordinal));
            if (paymentCount > 0)
            {
                throw new ApiException(
                    409,
                    "Đơn hàng đã có thanh toán. Hãy xoá các khoản thanh toán trước khi xoá đơn.",
                    "order_has_payments");
            }

            data.Orders.Remove(order);
            StudioAudit.Add(
                data,
                actor,
                SalesModule,
                "delete",
                $"đã xoá đơn hàng {order.Code}",
                order.Id);
        });

        return Results.NoContent();
    }

    private static IResult GetInvoice(string id, DataStore store)
    {
        var payload = store.Read(data => ApiMappings.ToInvoice(FindOrder(data, id), data));
        return Results.Ok(payload);
    }

    // -------------------------------------------------------------- payments

    private static IResult RecordPayment(
        string id,
        RecordPaymentRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var amount = ApiMappings.RoundMoney(request.Amount);
        if (amount <= 0)
        {
            throw new ApiException(400, "Số tiền thanh toán phải lớn hơn 0.", "invalid_payment_amount");
        }

        var method = NormalizePaymentMethodOrThrow(request.Method);
        var paidAt = AlbumText.NormalizeEventDate(request.PaidAt);
        if (paidAt.Length == 0)
        {
            paidAt = Today();
        }

        var dto = store.Write(data =>
        {
            var order = FindOrder(data, id);
            if (string.Equals(order.Status, OrderStatuses.Cancelled, StringComparison.Ordinal))
            {
                throw new ApiException(
                    409,
                    "Không thể ghi thanh toán cho đơn hàng đã huỷ.",
                    "order_cancelled");
            }

            var paymentSequence = data.PaymentSequence;
            var payment = new Payment
            {
                Id = NewShortId(),
                Code = NextCode("PT-", data.Payments.Select(item => item.Code), ref paymentSequence),
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                Amount = amount,
                Method = method,
                PaidAt = paidAt,
                Note = AlbumText.TrimTo(request.Note, 500),
                ActorId = actor.Id,
                ActorName = string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.Username : actor.DisplayName,
                CreatedAt = DateTimeOffset.UtcNow
            };
            data.Payments.Add(payment);
            data.PaymentSequence = paymentSequence;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                SalesModule,
                "payment",
                $"đã ghi thanh toán {payment.Code} cho đơn {order.Code}",
                order.Id,
                [$"Số tiền: {FormatMoney(amount)}", $"Hình thức: {PaymentMethodLabel(method)}"]);
            StudioNotifications.Push(
                data,
                NotificationTypes.Payment,
                $"Đã thu {FormatMoney(amount)}",
                $"Đơn {order.Code} — {CustomerName(data, order.CustomerId)} — {PaymentMethodLabel(method)}.",
                "/admin/orders");
            return new OrderDetailDto(
                ApiMappings.ToOrder(order, data),
                data.Payments
                    .Where(item => string.Equals(item.OrderId, order.Id, StringComparison.Ordinal))
                    .OrderByDescending(item => item.PaidAt, StringComparer.Ordinal)
                    .ThenByDescending(item => item.CreatedAt)
                    .Select(item => ApiMappings.ToPayment(item, data))
                    .ToList());
        });

        return Results.Ok(dto);
    }

    private static IResult DeletePayment(
        string id,
        string paymentId,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var dto = store.Write(data =>
        {
            var order = FindOrder(data, id);
            var payment = data.Payments.FirstOrDefault(item =>
                string.Equals(item.Id, paymentId, StringComparison.Ordinal) &&
                string.Equals(item.OrderId, order.Id, StringComparison.Ordinal));
            if (payment is null)
            {
                throw new ApiException(404, "Không tìm thấy khoản thanh toán.", "payment_not_found");
            }

            data.Payments.Remove(payment);
            order.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                SalesModule,
                "payment_delete",
                $"đã xoá thanh toán {payment.Code} của đơn {order.Code}",
                order.Id,
                [$"Số tiền: {FormatMoney(payment.Amount)}"]);
            return new OrderDetailDto(
                ApiMappings.ToOrder(order, data),
                data.Payments
                    .Where(item => string.Equals(item.OrderId, order.Id, StringComparison.Ordinal))
                    .OrderByDescending(item => item.PaidAt, StringComparer.Ordinal)
                    .ThenByDescending(item => item.CreatedAt)
                    .Select(item => ApiMappings.ToPayment(item, data))
                    .ToList());
        });

        return Results.Ok(dto);
    }

    // ------------------------------------------------------------- contracts

    private static IResult GetContracts(DataStore store)
    {
        var today = Today();
        var payload = store.Read(data => data.Contracts
            .OrderByDescending(contract => contract.CreatedAt)
            .ThenByDescending(contract => contract.Code, StringComparer.OrdinalIgnoreCase)
            .Select(contract => ApiMappings.ToContract(contract, data, today))
            .ToList());
        return Results.Ok(payload);
    }

    private static IResult GetContract(string id, DataStore store)
    {
        var today = Today();
        var payload = store.Read(data =>
            ApiMappings.ToContract(FindContract(data, id), data, today));
        return Results.Ok(payload);
    }

    private static IResult ExportContracts(string? status, string? q, DataStore store)
    {
        var today = Today();
        var all = store.Read(data => data.Contracts
            .OrderByDescending(contract => contract.CreatedAt)
            .ThenByDescending(contract => contract.Code, StringComparer.OrdinalIgnoreCase)
            .Select(contract => ApiMappings.ToContract(contract, data, today))
            .ToList());

        var statusKey = string.IsNullOrWhiteSpace(status) ||
                        string.Equals(status, "all", StringComparison.OrdinalIgnoreCase)
            ? null
            : status.Trim().ToLowerInvariant();
        var needle = (q ?? "").Trim();
        var contracts = all
            .Where(contract => statusKey is null ||
                               string.Equals(contract.Status, statusKey, StringComparison.Ordinal))
            .Where(contract => needle.Length == 0 ||
                               ContainsText(contract.Code, needle) ||
                               ContainsText(contract.CustomerName, needle) ||
                               ContainsText(contract.Service, needle) ||
                               ContainsText(contract.BookingCode, needle))
            .ToList();

        var workbook = new ExcelWorkbook();
        var sheet = workbook.AddSheet(
            "Hợp đồng",
            new ExcelColumn("Mã hợp đồng", Width: 16),
            new ExcelColumn("Khách hàng", Width: 24),
            new ExcelColumn("Số điện thoại", Width: 16),
            new ExcelColumn("Dịch vụ", Width: 28),
            new ExcelColumn("Booking", Width: 14),
            new ExcelColumn("Giá trị (đ)", ExcelColumnKind.Money, 16),
            new ExcelColumn("Đã thu (đ)", ExcelColumnKind.Money, 16),
            new ExcelColumn("Còn lại (đ)", ExcelColumnKind.Money, 16),
            new ExcelColumn("Tiến độ (%)", ExcelColumnKind.Number, 13),
            new ExcelColumn("Trạng thái", Width: 15),
            new ExcelColumn("Ngày tạo", Width: 13));

        foreach (var contract in contracts)
        {
            sheet.AddRow(
                contract.Code,
                contract.CustomerName ?? "Khách lẻ",
                contract.CustomerPhone ?? "",
                contract.Service,
                contract.BookingCode ?? "",
                contract.Total,
                contract.PaidAmount,
                contract.Remaining,
                contract.ProgressPercent,
                ContractStatusLabel(contract.Status),
                contract.CreatedAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        }

        sheet.AddBlankRow();
        sheet.AddTotalRow(
            "",
            "",
            "",
            "",
            "Tổng cộng",
            contracts.Sum(contract => contract.Total),
            contracts.Sum(contract => contract.PaidAmount),
            contracts.Sum(contract => contract.Remaining),
            "",
            "",
            "");

        return Results.File(
            workbook.Build(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"hop-dong-{Today().Replace("-", "")}.xlsx");
    }

    private static bool ContainsText(string? value, string needle) =>
        !string.IsNullOrEmpty(value) && value.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static IResult CreateContract(
        CreateContractRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var total = ApiMappings.RoundMoney(request.Total);
        if (total <= 0)
        {
            throw new ApiException(400, "Giá trị hợp đồng phải lớn hơn 0.", "invalid_contract_total");
        }

        var preset = NormalizePresetOrThrow(request.Preset);
        var status = NormalizeContractStatusOrThrow(request.Status);
        var now = DateTimeOffset.UtcNow;

        var dto = store.Write(data =>
        {
            var customer = ResolveCustomer(data, request.CustomerId, required: true)!;
            var booking = ResolveBooking(data, request.BookingId);
            var service = AlbumText.TrimTo(request.Service, 160);
            if (service.Length == 0)
            {
                service = data.Packages.FirstOrDefault(package =>
                    string.Equals(package.Id, booking?.PackageId, StringComparison.Ordinal))?.Name
                    ?? "Dịch vụ studio";
            }

            var schedule = BuildSchedule(total, preset, booking?.Date, now);
            var contractSequence = data.ContractSequence;
            var terms = AlbumText.TrimTo(request.Terms, 8000);
            if (terms.Length == 0)
            {
                terms = BuildTerms(customer.Name, service, total, schedule.Count > 0 ? schedule[0].Amount : 0);
            }

            var contract = new Contract
            {
                Id = NewShortId(),
                Code = NextCode("HD-", data.Contracts.Select(item => item.Code), ref contractSequence),
                CustomerId = customer.Id,
                BookingId = booking?.Id ?? "",
                Service = service,
                Total = total,
                Schedule = schedule,
                Terms = terms,
                Status = status,
                CreatedAt = now,
                UpdatedAt = now
            };
            data.Contracts.Add(contract);
            data.ContractSequence = contractSequence;
            StudioAudit.Add(
                data,
                actor,
                ContractModule,
                "create",
                $"đã tạo hợp đồng {contract.Code} cho “{customer.Name}”",
                contract.Id,
                [$"Giá trị: {FormatMoney(total)}", $"Lịch thanh toán: {preset}"]);
            StudioNotifications.Push(
                data,
                NotificationTypes.Contract,
                $"Hợp đồng mới: {contract.Code}",
                $"{customer.Name} — {service} — {FormatMoney(total)}.",
                "/admin/contracts");
            return ApiMappings.ToContract(contract, data, Today());
        });

        return Results.Created($"/api/admin/contracts/{dto.Id}", dto);
    }

    private static IResult UpdateContract(
        string id,
        UpdateContractRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var status = request.Status is null ? null : NormalizeContractStatusOrThrow(request.Status);
        var preset = request.Preset is null ? null : NormalizePresetOrThrow(request.Preset);

        var dto = store.Write(data =>
        {
            var contract = FindContract(data, id);
            var changes = new List<string>();

            if (request.CustomerId is not null)
            {
                var customer = ResolveCustomer(data, request.CustomerId, required: true)!;
                contract.CustomerId = customer.Id;
                changes.Add($"Khách hàng: {customer.Name}");
            }

            if (request.BookingId is not null)
            {
                var booking = ResolveBooking(data, request.BookingId);
                contract.BookingId = booking?.Id ?? "";
            }

            if (request.Service is not null)
            {
                contract.Service = AlbumText.TrimTo(request.Service, 160);
            }

            if (request.Terms is not null)
            {
                contract.Terms = AlbumText.TrimTo(request.Terms, 8000);
            }

            var rebuild = false;
            if (request.Total is not null)
            {
                var total = ApiMappings.RoundMoney(request.Total.Value);
                if (total <= 0)
                {
                    throw new ApiException(400, "Giá trị hợp đồng phải lớn hơn 0.", "invalid_contract_total");
                }

                contract.Total = total;
                rebuild = true;
                changes.Add($"Giá trị: {FormatMoney(total)}");
            }

            if (preset is not null)
            {
                rebuild = true;
                changes.Add($"Lịch thanh toán: {preset}");
            }

            if (rebuild)
            {
                var booking = data.Bookings.FirstOrDefault(item =>
                    string.Equals(item.Id, contract.BookingId, StringComparison.Ordinal));
                contract.Schedule = BuildSchedule(
                    contract.Total,
                    preset ?? InferPreset(contract.Schedule),
                    booking?.Date,
                    contract.CreatedAt,
                    contract.Schedule);
            }

            if (status is not null && !string.Equals(contract.Status, status, StringComparison.Ordinal))
            {
                changes.Add($"Trạng thái: {ContractStatusLabel(contract.Status)} → {ContractStatusLabel(status)}");
                contract.Status = status;
            }

            contract.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                ContractModule,
                "update",
                $"đã cập nhật hợp đồng {contract.Code}",
                contract.Id,
                changes);
            if (changes.Count > 0)
            {
                StudioNotifications.Push(
                    data,
                    NotificationTypes.Contract,
                    $"Hợp đồng {contract.Code} đã cập nhật",
                    changes[0],
                    "/admin/contracts");
            }
            return ApiMappings.ToContract(contract, data, Today());
        });

        return Results.Ok(dto);
    }

    private static IResult ToggleSchedule(
        string id,
        int index,
        ToggleContractScheduleRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var dto = store.Write(data =>
        {
            var contract = FindContract(data, id);
            if (index < 0 || index >= contract.Schedule.Count)
            {
                throw new ApiException(404, "Không tìm thấy mốc thanh toán.", "schedule_not_found");
            }

            var item = contract.Schedule[index];
            item.Paid = request.Paid;
            item.PaidAt = request.Paid
                ? (item.PaidAt.Length > 0 ? item.PaidAt : Today())
                : "";
            contract.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                ContractModule,
                request.Paid ? "schedule_paid" : "schedule_unpaid",
                $"{(request.Paid ? "đã đánh dấu" : "đã bỏ đánh dấu")} thanh toán “{item.Label}” của hợp đồng {contract.Code}",
                contract.Id,
                [$"Số tiền: {FormatMoney(item.Amount)}"]);
            return ApiMappings.ToContract(contract, data, Today());
        });

        return Results.Ok(dto);
    }

    private static IResult DeleteContract(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        store.Write(data =>
        {
            var contract = FindContract(data, id);
            data.Contracts.Remove(contract);
            StudioAudit.Add(
                data,
                actor,
                ContractModule,
                "delete",
                $"đã xoá hợp đồng {contract.Code}",
                contract.Id);
        });

        return Results.NoContent();
    }

    // --------------------------------------------------------------- reports

    private static IResult GetReportSummary(string? from, string? to, DataStore store)
    {
        var range = ResolveRange(from, to);
        var payload = store.Read(data => BuildReport(data, range));
        return Results.Ok(payload);
    }

    private static IResult GetReportOverview(DataStore store)
    {
        var payload = store.Read(data =>
        {
            var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var monthKey = monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var orderRevenue = OrderRevenue(data, monthKey + "-01", today);
            var contractRevenue = ContractRevenue(data, monthKey + "-01", today);
            var openOrders = data.Orders
                .Where(order => !string.Equals(order.Status, OrderStatuses.Cancelled, StringComparison.Ordinal))
                .ToList();
            var openContracts = data.Contracts
                .Where(contract => !string.Equals(contract.Status, ContractStatuses.Cancelled, StringComparison.Ordinal))
                .ToList();

            return new SalesOverviewDto(
                data.Orders.Count,
                openOrders.Count(order => order.Status != OrderStatuses.Completed),
                orderRevenue + contractRevenue,
                openOrders.Sum(order => OrderRemaining(order, data)),
                data.Contracts.Count,
                openContracts.Count(contract => contract.Status is ContractStatuses.Draft or ContractStatuses.Pending),
                openContracts.Sum(contract => ContractRemaining(contract)));
        });

        return Results.Ok(payload);
    }

    private static ReportSummaryDto BuildReport(AppData data, (string From, string To) range)
    {
        var today = Today();
        var orderRevenue = OrderRevenue(data, range.From, range.To);
        var contractRevenue = ContractRevenue(data, range.From, range.To);
        var activeOrders = data.Orders
            .Where(order => !string.Equals(order.Status, OrderStatuses.Cancelled, StringComparison.Ordinal))
            .ToList();
        var activeContracts = data.Contracts
            .Where(contract => !string.Equals(contract.Status, ContractStatuses.Cancelled, StringComparison.Ordinal))
            .ToList();
        var orderDebt = activeOrders.Sum(order => OrderRemaining(order, data));
        var contractDebt = activeContracts.Sum(ContractRemaining);

        var paymentsInRange = data.Payments
            .Where(payment => InRange(payment.PaidAt, range.From, range.To))
            .Where(payment => data.Orders.Any(order =>
                string.Equals(order.Id, payment.OrderId, StringComparison.Ordinal) &&
                !string.Equals(order.Status, OrderStatuses.Cancelled, StringComparison.Ordinal)))
            .ToList();
        var methods = paymentsInRange
            .GroupBy(payment => payment.Method, StringComparer.Ordinal)
            .Select(group => new ReportMethodDto(
                group.Key,
                group.Count(),
                ApiMappings.RoundMoney(group.Sum(payment => payment.Amount))))
            .OrderByDescending(item => item.Amount)
            .ToList();

        var revenueByCustomer = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var payment in paymentsInRange)
        {
            if (payment.CustomerId.Length == 0)
            {
                continue;
            }

            revenueByCustomer[payment.CustomerId] =
                revenueByCustomer.GetValueOrDefault(payment.CustomerId) + payment.Amount;
        }

        foreach (var contract in activeContracts)
        {
            foreach (var item in contract.Schedule.Where(item => item.Paid && InRange(item.PaidAt, range.From, range.To)))
            {
                if (contract.CustomerId.Length == 0)
                {
                    continue;
                }

                revenueByCustomer[contract.CustomerId] =
                    revenueByCustomer.GetValueOrDefault(contract.CustomerId) + item.Amount;
            }
        }

        var topCustomers = revenueByCustomer
            .OrderByDescending(pair => pair.Value)
            .Take(5)
            .Select(pair => new ReportCustomerDto(
                pair.Key,
                CustomerName(data, pair.Key),
                ApiMappings.RoundMoney(pair.Value),
                activeOrders.Count(order => string.Equals(order.CustomerId, pair.Key, StringComparison.Ordinal))))
            .ToList();

        var months = new List<ReportMonthDto>();
        if (DateTime.TryParseExact(range.From[..7] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var cursor) &&
            DateTime.TryParseExact(range.To[..7] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var lastMonth))
        {
            var guard = 0;
            while (cursor <= lastMonth && guard++ < 24)
            {
                var key = cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                var monthFrom = cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var monthTo = cursor.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var monthOrder = OrderRevenue(data, monthFrom, monthTo);
                var monthContract = ContractRevenue(data, monthFrom, monthTo);
                months.Add(new ReportMonthDto(key, monthOrder, monthContract, monthOrder + monthContract));
                cursor = cursor.AddMonths(1);
            }
        }

        return new ReportSummaryDto(
            range.From,
            range.To,
            orderRevenue,
            contractRevenue,
            orderRevenue + contractRevenue,
            activeOrders.Count(order => InRange(
                order.CreatedAt.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                range.From,
                range.To)),
            activeOrders.Count(order => OrderPaid(order, data) >= OrderTotal(order)),
            activeOrders.Count(order => OrderPaid(order, data) <= 0),
            orderDebt,
            ApiMappings.RoundMoney(activeContracts.Sum(contract => contract.Schedule.Where(item => item.Paid).Sum(item => item.Amount))),
            contractDebt,
            activeContracts.Count,
            activeContracts.Count(contract =>
                contract.Status is ContractStatuses.Draft or ContractStatuses.Pending),
            months,
            methods,
            topCustomers);
    }

    private static decimal OrderRevenue(AppData data, string from, string to)
    {
        var activeOrders = data.Orders
            .Where(order => !string.Equals(order.Status, OrderStatuses.Cancelled, StringComparison.Ordinal))
            .Select(order => order.Id)
            .ToHashSet(StringComparer.Ordinal);
        return ApiMappings.RoundMoney(data.Payments
            .Where(payment => activeOrders.Contains(payment.OrderId) && InRange(payment.PaidAt, from, to))
            .Sum(payment => payment.Amount));
    }

    private static decimal ContractRevenue(AppData data, string from, string to) =>
        ApiMappings.RoundMoney(data.Contracts
            .Where(contract => !string.Equals(contract.Status, ContractStatuses.Cancelled, StringComparison.Ordinal))
            .SelectMany(contract => contract.Schedule)
            .Where(item => item.Paid && InRange(item.PaidAt, from, to))
            .Sum(item => item.Amount));

    private static decimal OrderTotal(Order order) =>
        Math.Max(0, ApiMappings.RoundMoney(
            order.Items.Sum(item => item.Quantity * item.UnitPrice) - order.Discount));

    private static decimal OrderPaid(Order order, AppData data) =>
        ApiMappings.RoundMoney(data.Payments
            .Where(payment => string.Equals(payment.OrderId, order.Id, StringComparison.Ordinal))
            .Sum(payment => payment.Amount));

    private static decimal OrderRemaining(Order order, AppData data) =>
        Math.Max(0, OrderTotal(order) - OrderPaid(order, data));

    private static decimal ContractRemaining(Contract contract) =>
        Math.Max(0, contract.Total - ApiMappings.RoundMoney(
            contract.Schedule.Where(item => item.Paid).Sum(item => item.Amount)));

    private static (string From, string To) ResolveRange(string? from, string? to)
    {
        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
        var fallbackFrom = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var fromDate = ParseDate(from) ?? fallbackFrom;
        var toDate = ParseDate(to) ?? now.Date;
        if (fromDate > toDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        if ((toDate - fromDate).TotalDays > 731)
        {
            fromDate = toDate.AddDays(-731);
        }

        return (
            fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    private static DateTime? ParseDate(string? value)
    {
        var normalized = AlbumText.NormalizeEventDate(value);
        return DateTime.TryParseExact(normalized, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static bool InRange(string date, string from, string to) =>
        date.Length >= 10 &&
        string.CompareOrdinal(date[..10], from) >= 0 &&
        string.CompareOrdinal(date[..10], to) <= 0;

    // --------------------------------------------------------------- helpers

    private static List<OrderItem> NormalizeItemsOrThrow(IEnumerable<OrderItemRequest>? items)
    {
        var normalized = new List<OrderItem>();
        foreach (var item in items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            var name = AlbumText.TrimTo(item.Name, 160);
            var quantity = item.Quantity;
            if (name.Length == 0)
            {
                throw new ApiException(400, "Tên hạng mục không được để trống.", "invalid_order_item");
            }

            if (quantity <= 0 || quantity > 10000)
            {
                throw new ApiException(400, "Số lượng hạng mục không hợp lệ.", "invalid_order_quantity");
            }

            if (item.UnitPrice < 0)
            {
                throw new ApiException(400, "Đơn giá không được âm.", "invalid_order_price");
            }

            normalized.Add(new OrderItem
            {
                Name = name,
                Quantity = quantity,
                UnitPrice = ApiMappings.RoundMoney(item.UnitPrice)
            });
        }

        if (normalized.Count == 0)
        {
            throw new ApiException(400, "Đơn hàng cần ít nhất một hạng mục.", "empty_order");
        }

        return normalized;
    }

    private static string NormalizeOrderStatusOrThrow(string? value)
    {
        var status = (value ?? "").Trim().ToLowerInvariant();
        if (status.Length == 0)
        {
            return OrderStatuses.Confirmed;
        }

        if (!OrderStatuses.IsKnown(status))
        {
            throw new ApiException(400, "Trạng thái đơn hàng không hợp lệ.", "invalid_order_status");
        }

        return status;
    }

    private static string NormalizeContractStatusOrThrow(string? value)
    {
        var status = (value ?? "").Trim().ToLowerInvariant();
        if (status.Length == 0)
        {
            return ContractStatuses.Pending;
        }

        if (!ContractStatuses.IsKnown(status))
        {
            throw new ApiException(400, "Trạng thái hợp đồng không hợp lệ.", "invalid_contract_status");
        }

        return status;
    }

    private static string NormalizePaymentMethodOrThrow(string? value)
    {
        var method = (value ?? "").Trim().ToLowerInvariant();
        if (method.Length == 0)
        {
            return PaymentMethods.Transfer;
        }

        if (!PaymentMethods.IsKnown(method))
        {
            throw new ApiException(400, "Hình thức thanh toán không hợp lệ.", "invalid_payment_method");
        }

        return method;
    }

    private static string NormalizePresetOrThrow(string? value)
    {
        var preset = (value ?? "").Trim();
        if (preset.Length == 0)
        {
            return "30/40/30";
        }

        if (!SchedulePresets.Contains(preset, StringComparer.Ordinal))
        {
            throw new ApiException(400, "Tỷ lệ thanh toán không hợp lệ.", "invalid_contract_preset");
        }

        return preset;
    }

    private static List<ContractScheduleItem> BuildSchedule(
        decimal total,
        string preset,
        string? bookingDate,
        DateTimeOffset createdAt,
        List<ContractScheduleItem>? previous = null)
    {
        var percentages = preset
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => decimal.TryParse(part, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0)
            .Where(value => value > 0)
            .ToList();
        if (percentages.Count == 0)
        {
            percentages = [30, 40, 30];
        }

        var labels = percentages.Count == 2
            ? new[] { "Đặt cọc", "Thanh toán còn lại" }
            : new[] { "Đặt cọc", "Sau buổi chụp", "Giao ảnh & sản phẩm" };
        var created = createdAt == default
            ? DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7))
            : createdAt.ToOffset(TimeSpan.FromHours(7));
        var firstDue = created.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var normalizedBooking = AlbumText.NormalizeEventDate(bookingDate);
        var middleDue = normalizedBooking.Length > 0
            ? normalizedBooking
            : created.AddDays(14).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var lastDue = (DateTime.TryParseExact(middleDue, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var middle)
            ? middle.AddDays(14)
            : created.AddDays(28)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dues = new[] { firstDue, middleDue, lastDue };

        var schedule = new List<ContractScheduleItem>();
        var allocated = 0m;
        for (var index = 0; index < percentages.Count; index++)
        {
            var amount = index == percentages.Count - 1
                ? total - allocated
                : ApiMappings.RoundMoney(total * percentages[index] / 100);
            allocated += amount;
            var existing = previous is not null && index < previous.Count ? previous[index] : null;
            schedule.Add(new ContractScheduleItem
            {
                Label = $"{(index < labels.Length ? labels[index] : "Thanh toán")} ({percentages[index]:0.#}%)",
                Amount = Math.Max(0, amount),
                DueDate = existing is not null && existing.DueDate.Length > 0
                    ? existing.DueDate
                    : dues[Math.Min(index, dues.Length - 1)],
                Paid = existing?.Paid ?? false,
                PaidAt = existing?.Paid == true ? existing.PaidAt : ""
            });
        }

        return schedule;
    }

    private static string InferPreset(List<ContractScheduleItem> schedule)
    {
        if (schedule.Count == 0)
        {
            return "30/40/30";
        }

        var total = schedule.Sum(item => item.Amount);
        if (total <= 0)
        {
            return "30/40/30";
        }

        var preset = string.Join("/", schedule.Select(item =>
            ((int)Math.Round(item.Amount / total * 100, MidpointRounding.AwayFromZero)).ToString(
                CultureInfo.InvariantCulture)));
        return SchedulePresets.Contains(preset, StringComparer.Ordinal) ? preset : "30/40/30";
    }

    private static string BuildTerms(string customerName, string service, decimal total, decimal deposit) =>
        string.Join("\n",
        [
            $"1. Studio cam kết cung cấp dịch vụ “{service}” đúng chất lượng và tiến độ đã thỏa thuận với khách hàng {customerName}.",
            $"2. Khách hàng đặt cọc {FormatMoney(deposit)} để giữ lịch; tiền cọc không hoàn lại nếu huỷ lịch trước ít hơn 7 ngày.",
            $"3. Tổng giá trị hợp đồng là {FormatMoney(total)}; số tiền còn lại thanh toán đúng theo lịch thanh toán đính kèm.",
            "4. Thời gian giao ảnh chỉnh sửa: 10–14 ngày làm việc kể từ buổi chụp; album & phóng ảnh: 20–30 ngày làm việc.",
            "5. Studio được sử dụng một phần ảnh thành phẩm cho mục đích quảng bá, trừ khi khách hàng có yêu cầu riêng bằng văn bản.",
            "6. Việc đổi lịch chụp cần báo trước ít nhất 48 giờ và phụ thuộc khả năng sắp xếp của studio.",
            "7. Hai bên cam kết thực hiện đúng các điều khoản; mọi tranh chấp thỏa thuận trên tinh thần hợp tác."
        ]);

    private static Customer? ResolveCustomer(AppData data, string? customerId, bool required)
    {
        var id = AlbumText.TrimTo(customerId, 40);
        var customer = id.Length == 0
            ? null
            : data.Customers.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        if (customer is null && required)
        {
            throw new ApiException(400, "Vui lòng chọn khách hàng.", "customer_required");
        }

        return customer;
    }

    private static Booking? ResolveBooking(AppData data, string? bookingId)
    {
        var id = AlbumText.TrimTo(bookingId, 40);
        return id.Length == 0
            ? null
            : data.Bookings.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
    }

    private static Order FindOrder(AppData data, string id) =>
        data.Orders.FirstOrDefault(order => string.Equals(order.Id, id, StringComparison.Ordinal))
        ?? throw new ApiException(404, "Không tìm thấy đơn hàng.", "order_not_found");

    private static Contract FindContract(AppData data, string id) =>
        data.Contracts.FirstOrDefault(contract => string.Equals(contract.Id, id, StringComparison.Ordinal))
        ?? throw new ApiException(404, "Không tìm thấy hợp đồng.", "contract_not_found");

    private static string CustomerName(AppData data, string customerId) =>
        data.Customers.FirstOrDefault(customer =>
            string.Equals(customer.Id, customerId, StringComparison.Ordinal))?.Name ?? "Khách lẻ";

    private static string NextCode(string prefix, IEnumerable<string> codes, ref int sequence)
    {
        var used = codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string candidate;
        do
        {
            candidate = $"{prefix}{sequence:0000}";
            sequence++;
        }
        while (used.Contains(candidate));

        return candidate;
    }

    private static string Today() =>
        DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatMoney(decimal value) =>
        $"{value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đ";

    private static string NewShortId() => Guid.NewGuid().ToString("N")[..12];

    private static string OrderStatusLabel(string status) => status switch
    {
        OrderStatuses.Draft => "Nháp",
        OrderStatuses.Confirmed => "Đã xác nhận",
        OrderStatuses.Completed => "Hoàn thành",
        OrderStatuses.Cancelled => "Đã huỷ",
        _ => status
    };

    private static string ContractStatusLabel(string status) => status switch
    {
        ContractStatuses.Draft => "Nháp",
        ContractStatuses.Pending => "Chờ ký",
        ContractStatuses.Signed => "Đã ký",
        ContractStatuses.Completed => "Hoàn thành",
        ContractStatuses.Cancelled => "Đã huỷ",
        _ => status
    };

    private static string PaymentMethodLabel(string method) => method switch
    {
        PaymentMethods.Cash => "Tiền mặt",
        PaymentMethods.Transfer => "Chuyển khoản",
        PaymentMethods.Card => "Thẻ",
        PaymentMethods.Other => "Khác",
        _ => method
    };
}
