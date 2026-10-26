using System.Globalization;
using System.Text;
using MayAlbum.Models;
using MayAlbum.Services;

namespace MayAlbum.Api;

/// <summary>
/// Phase 3 vận hành studio: chi phí, bảng lương, chấm công, tủ trang phục,
/// concept và hộp thư thông báo. Mọi thay đổi đều ghi nhật ký studio.
/// </summary>
public static class OperationsEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        var finance = new StudioPermissionFilter(StudioPermissions.Finance);
        var attendance = new StudioPermissionFilter(StudioPermissions.Attendance);
        var wardrobe = new StudioPermissionFilter(StudioPermissions.Wardrobe);
        var notifications = new StudioPermissionFilter(StudioPermissions.Notifications);

        // ---------------------------------------------------------- finance
        group.MapGet("/finance/overview", GetFinanceOverview).AddEndpointFilter(finance);
        group.MapGet("/finance/entries", GetFinanceEntries).AddEndpointFilter(finance);
        group.MapPost("/finance/entries", CreateFinanceEntry).AddEndpointFilter(finance);
        group.MapPut("/finance/entries/{id}", UpdateFinanceEntry).AddEndpointFilter(finance);
        group.MapDelete("/finance/entries/{id}", DeleteFinanceEntry).AddEndpointFilter(finance);
        group.MapPost("/finance/entries/{id}/photos", UploadFinancePhoto).AddEndpointFilter(finance).DisableAntiforgery();
        group.MapDelete("/finance/entries/{id}/photos/{photoId}", DeleteFinancePhoto).AddEndpointFilter(finance);
        group.MapGet("/finance/photos/{photoId}", GetFinancePhoto).AddEndpointFilter(finance);
        group.MapGet("/finance/entries/export", ExportFinanceEntries).AddEndpointFilter(finance);
        group.MapGet("/finance/export", ExportFinanceWorkbook).AddEndpointFilter(finance);
        group.MapGet("/expenses", GetExpenses).AddEndpointFilter(finance);
        group.MapPost("/expenses", CreateExpense).AddEndpointFilter(finance);
        group.MapPut("/expenses/{id}", UpdateExpense).AddEndpointFilter(finance);
        group.MapDelete("/expenses/{id}", DeleteExpense).AddEndpointFilter(finance);
        group.MapGet("/payrolls", GetPayrolls).AddEndpointFilter(finance);
        group.MapPost("/payrolls/compute", ComputePayrolls).AddEndpointFilter(finance);
        group.MapPut("/payrolls/{id}/paid", TogglePayrollPaid).AddEndpointFilter(finance);

        // ------------------------------------------------------- attendance
        group.MapGet("/attendance", GetAttendance).AddEndpointFilter(attendance);
        group.MapGet("/attendance/month", GetAttendanceMonth).AddEndpointFilter(attendance);
        group.MapPost("/attendance", CreateAttendance).AddEndpointFilter(attendance);
        group.MapPut("/attendance/{id}", UpdateAttendance).AddEndpointFilter(attendance);
        group.MapDelete("/attendance/{id}", DeleteAttendance).AddEndpointFilter(attendance);

        // ------------------------------------------------------ shift planner
        group.MapGet("/shifts", GetShiftWeek).AddEndpointFilter(attendance);
        group.MapPut("/shifts", SaveShift).AddEndpointFilter(attendance);
        group.MapDelete("/shifts", DeleteShift).AddEndpointFilter(attendance);
        group.MapPost("/shifts/fill", FillShiftWeek).AddEndpointFilter(attendance);
        group.MapDelete("/shifts/week", ClearShiftWeek).AddEndpointFilter(attendance);

        // --------------------------------------------------------- wardrobe
        group.MapGet("/outfits", GetOutfits).AddEndpointFilter(wardrobe);
        group.MapPost("/outfits", CreateOutfit).AddEndpointFilter(wardrobe);
        group.MapPut("/outfits/{id}", UpdateOutfit).AddEndpointFilter(wardrobe);
        group.MapDelete("/outfits/{id}", DeleteOutfit).AddEndpointFilter(wardrobe);
        group.MapGet("/concepts", GetConcepts).AddEndpointFilter(wardrobe);
        group.MapPost("/concepts", CreateConcept).AddEndpointFilter(wardrobe);
        group.MapPut("/concepts/{id}", UpdateConcept).AddEndpointFilter(wardrobe);
        group.MapDelete("/concepts/{id}", DeleteConcept).AddEndpointFilter(wardrobe);
        group.MapGet("/loans", GetLoans).AddEndpointFilter(wardrobe);
        group.MapPost("/loans", CreateLoan).AddEndpointFilter(wardrobe);
        group.MapPut("/loans/{id}", UpdateLoan).AddEndpointFilter(wardrobe);
        group.MapPut("/loans/{id}/return", ReturnLoan).AddEndpointFilter(wardrobe);
        group.MapDelete("/loans/{id}", DeleteLoan).AddEndpointFilter(wardrobe);

        // ---------------------------------------------------- notifications
        group.MapGet("/notifications", GetNotifications).AddEndpointFilter(notifications);
        group.MapPut("/notifications/{id}/read", MarkNotificationRead).AddEndpointFilter(notifications);
        group.MapPost("/notifications/read-all", MarkAllNotificationsRead).AddEndpointFilter(notifications);
        group.MapDelete("/notifications/{id}", DeleteNotification).AddEndpointFilter(notifications);
        group.MapGet("/notifications/settings", GetNotificationSettings).AddEndpointFilter(notifications);
        group.MapPut("/notifications/settings", UpdateNotificationSettings).AddEndpointFilter(notifications);
    }

    // --------------------------------------------------------------- finance

    private static IResult GetFinanceOverview(string? month, DataStore store)
    {
        var key = ResolveMonth(month);
        var payload = store.Read(data => BuildFinanceOverview(data, key));
        return Results.Ok(payload);
    }

    private static IResult GetFinanceEntries(string? month, string? kind, DataStore store)
    {
        var payload = store.Read(data =>
        {
            var query = data.Expenses.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(month))
            {
                var key = ResolveMonth(month);
                query = query.Where(entry => entry.Date.StartsWith(key, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(kind) &&
                !string.Equals(kind, "all", StringComparison.OrdinalIgnoreCase))
            {
                var normalized = ParseFinanceKind(kind);
                query = query.Where(entry =>
                    string.Equals(FinanceEntryKinds.Normalize(entry.Kind), normalized, StringComparison.Ordinal));
            }

            return query
                .OrderByDescending(entry => entry.Date, StringComparer.Ordinal)
                .ThenByDescending(entry => entry.CreatedAt)
                .Select(ApiMappings.ToFinanceEntry)
                .ToList();
        });
        return Results.Ok(payload);
    }

    private static IResult CreateFinanceEntry(
        CreateFinanceEntryRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var kind = ParseFinanceKind(request.Kind);
        var date = RequireDate(request.Date, kind == FinanceEntryKinds.Income ? "Ngày thu" : "Ngày chi");
        var amount = Math.Max(0, request.Amount);
        if (amount <= 0)
        {
            throw new ApiException(
                400,
                kind == FinanceEntryKinds.Income ? "Số tiền thu phải lớn hơn 0." : "Số tiền chi phải lớn hơn 0.",
                "invalid_finance_amount");
        }

        var now = DateTimeOffset.UtcNow;
        var dto = store.Write(data =>
        {
            var entry = new ExpenseEntry
            {
                Id = NewShortId(),
                Kind = kind,
                Date = date,
                Category = NormalizeFinanceCategory(kind, request.Category),
                Amount = ApiMappings.RoundMoney(amount),
                Note = AlbumText.TrimTo(request.Note, 500),
                CreatedById = actor.Id,
                CreatedByName = ActorName(actor),
                CreatedAt = now,
                UpdatedAt = now
            };
            data.Expenses.Add(entry);
            StudioAudit.Add(
                data,
                actor,
                "finance",
                kind == FinanceEntryKinds.Income ? "income" : "create",
                kind == FinanceEntryKinds.Income
                    ? $"đã ghi khoản thu {FormatMoney(entry.Amount)} ({entry.Category})"
                    : $"đã ghi chi phí {FormatMoney(entry.Amount)} ({entry.Category})",
                entry.Id,
                [entry.Note]);
            return ApiMappings.ToFinanceEntry(entry);
        });

        return Results.Created($"/api/admin/finance/entries/{dto.Id}", dto);
    }

    private static IResult UpdateFinanceEntry(
        string id,
        UpdateFinanceEntryRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var kind = request.Kind is null ? null : ParseFinanceKind(request.Kind);
        var date = request.Date is null ? null : RequireDate(request.Date, "Ngày thu chi");
        // Validate before changing the live in-memory entry. A rejected edit
        // must not change its kind/category and leak into a later save.
        if (request.Amount is <= 0)
        {
            throw new ApiException(400, "Số tiền thu chi phải lớn hơn 0.", "invalid_finance_amount");
        }

        var dto = store.Write(data =>
        {
            var entry = FindFinanceEntry(data, id);
            var changes = new List<string>();
            if (kind is not null && !string.Equals(entry.Kind, kind, StringComparison.Ordinal))
            {
                entry.Kind = kind;
                changes.Add($"Loại: {FinanceEntryKinds.Label(kind)}");
            }

            if (date is not null)
            {
                entry.Date = date;
                changes.Add($"Ngày: {date}");
            }

            if (request.Category is not null)
            {
                entry.Category = NormalizeFinanceCategory(entry.Kind, request.Category);
                changes.Add($"Danh mục: {entry.Category}");
            }

            if (request.Amount is not null)
            {
                entry.Amount = ApiMappings.RoundMoney(request.Amount.Value);
                changes.Add($"Số tiền: {FormatMoney(entry.Amount)}");
            }

            if (request.Note is not null)
            {
                entry.Note = AlbumText.TrimTo(request.Note, 500);
            }

            if (request.Photos is not null)
            {
                var photos = entry.Photos.ToDictionary(photo => photo.Id, StringComparer.Ordinal);
                foreach (var item in request.Photos)
                {
                    if (!photos.TryGetValue(item.Id, out var photo))
                    {
                        continue;
                    }

                    var caption = AlbumText.TrimTo(item.Caption, 120);
                    if (!string.Equals(photo.Caption, caption, StringComparison.Ordinal))
                    {
                        photo.Caption = caption;
                        changes.Add($"Chú thích ảnh: {caption}");
                    }
                }
            }

            entry.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "finance",
                "update",
                $"đã cập nhật {FinanceEntryKinds.Label(entry.Kind).ToLowerInvariant()} {FormatMoney(entry.Amount)}",
                entry.Id,
                changes);
            return ApiMappings.ToFinanceEntry(entry);
        });

        return Results.Ok(dto);
    }

    private static async Task<IResult> DeleteFinanceEntry(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions,
        IObjectStorage storage)
    {
        var actor = sessions.RequireUser(context);
        var files = store.Write(data =>
        {
            var entry = FindFinanceEntry(data, id);
            data.Expenses.Remove(entry);
            StudioAudit.Add(
                data,
                actor,
                "finance",
                "delete",
                $"đã xoá {FinanceEntryKinds.Label(entry.Kind).ToLowerInvariant()} {FormatMoney(entry.Amount)} ({entry.Category})",
                entry.Id);
            return entry.Photos
                .Select(photo => Path.GetFileName(photo.FileName))
                .Where(name => name.Length > 0)
                .ToList();
        });

        foreach (var file in files)
        {
            await DeleteFinancePhotoFile(storage, file, context.RequestAborted);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> UploadFinancePhoto(
        string id,
        IFormFile? file,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions,
        IObjectStorage storage)
    {
        var actor = sessions.RequireUser(context);
        if (file is null || file.Length == 0)
        {
            throw new ApiException(400, "Chọn một ảnh chứng từ để tải lên.", "invalid_finance_photo");
        }

        if (file.Length > MaxFinancePhotoBytes)
        {
            throw new ApiException(400, "Ảnh chứng từ tối đa 5 MB.", "finance_photo_too_large");
        }

        var originalName = Path.GetFileName(file.FileName ?? "chung-tu");
        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        if (!FinancePhotoTypes.TryGetValue(extension, out var contentType))
        {
            throw new ApiException(400, "Chỉ nhận ảnh JPG, PNG hoặc WebP.", "invalid_finance_photo_type");
        }

        byte[] bytes;
        await using (var memory = new MemoryStream())
        {
            await file.CopyToAsync(memory, context.RequestAborted);
            bytes = memory.ToArray();
        }

        if (!LooksLikeImage(bytes, extension))
        {
            throw new ApiException(400, "Tệp tải lên không phải ảnh hợp lệ.", "invalid_finance_photo");
        }

        var caption = context.Request.HasFormContentType
            ? AlbumText.TrimTo(context.Request.Form["caption"].ToString(), 120)
            : "";
        var storedName = Guid.NewGuid().ToString("N") + extension;
        await using (var upload = new MemoryStream(bytes, writable: false))
        {
            await storage.PutAsync(storedName, upload, contentType, context.RequestAborted);
        }

        try
        {
            var dto = store.Write(data =>
            {
                var entry = FindFinanceEntry(data, id);
                if (entry.Photos.Count >= MaxFinancePhotosPerEntry)
                {
                    throw new ApiException(
                        400,
                        $"Mỗi khoản thu chi chỉ gắn tối đa {MaxFinancePhotosPerEntry} ảnh.",
                        "finance_photo_limit");
                }

                var photo = new FinancePhoto
                {
                    Id = NewShortId(),
                    FileName = storedName,
                    OriginalName = AlbumText.TrimTo(originalName, 180),
                    ContentType = contentType,
                    Size = bytes.LongLength,
                    Caption = caption,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                entry.Photos.Add(photo);
                entry.UpdatedAt = DateTimeOffset.UtcNow;
                StudioAudit.Add(
                    data,
                    actor,
                    "finance",
                    "photo",
                    $"đã thêm ảnh chứng từ cho {FinanceEntryKinds.Label(entry.Kind).ToLowerInvariant()} {FormatMoney(entry.Amount)}",
                    entry.Id,
                    [caption]);
                return ApiMappings.ToFinanceEntry(entry).Photos.First(item => item.Id == photo.Id);
            });

            return Results.Created($"/api/admin/finance/photos/{dto.Id}", dto);
        }
        catch
        {
            await DeleteFinancePhotoFile(storage, storedName, context.RequestAborted);
            throw;
        }
    }

    private static async Task<IResult> DeleteFinancePhoto(
        string id,
        string photoId,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions,
        IObjectStorage storage)
    {
        var actor = sessions.RequireUser(context);
        var fileName = store.Write(data =>
        {
            var entry = FindFinanceEntry(data, id);
            var photo = entry.Photos.FirstOrDefault(item =>
                string.Equals(item.Id, photoId, StringComparison.Ordinal));
            if (photo is null)
            {
                throw new ApiException(404, "Không tìm thấy ảnh chứng từ.", "finance_photo_not_found");
            }

            entry.Photos.Remove(photo);
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "finance",
                "photo-delete",
                $"đã xoá ảnh chứng từ của {FinanceEntryKinds.Label(entry.Kind).ToLowerInvariant()} {FormatMoney(entry.Amount)}",
                entry.Id);
            return Path.GetFileName(photo.FileName);
        });

        await DeleteFinancePhotoFile(storage, fileName, context.RequestAborted);
        return Results.NoContent();
    }

    private static async Task<IResult> GetFinancePhoto(
        string photoId,
        HttpContext context,
        DataStore store,
        IObjectStorage storage)
    {
        var photo = store.Read(data => data.Expenses
            .SelectMany(entry => entry.Photos)
            .FirstOrDefault(item => string.Equals(item.Id, photoId, StringComparison.Ordinal)));
        if (photo is null)
        {
            return Results.NotFound();
        }

        var stored = await storage.GetAsync(photo.FileName, context.RequestAborted);
        if (stored is null)
        {
            return Results.NotFound();
        }

        context.Response.Headers.CacheControl = "private,max-age=3600";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.Stream(
            stored.Content,
            photo.ContentType.Length > 0 ? photo.ContentType : "application/octet-stream",
            enableRangeProcessing: true);
    }

    private static IResult ExportFinanceEntries(string? month, string? kind, DataStore store)
    {
        var key = ResolveMonth(month);
        var normalizedKind = string.IsNullOrWhiteSpace(kind) ||
                             string.Equals(kind, "all", StringComparison.OrdinalIgnoreCase)
            ? null
            : ParseFinanceKind(kind);
        var entries = store.Read(data => data.Expenses
            .Where(entry => entry.Date.StartsWith(key, StringComparison.Ordinal))
            .Where(entry => normalizedKind is null ||
                            string.Equals(FinanceEntryKinds.Normalize(entry.Kind), normalizedKind, StringComparison.Ordinal))
            .OrderByDescending(entry => entry.Date, StringComparer.Ordinal)
            .ThenByDescending(entry => entry.CreatedAt)
            .Select(ApiMappings.ToFinanceEntry)
            .ToList());

        var workbook = new ExcelWorkbook();
        AddFinanceEntrySheet(workbook, entries, "Thu chi");
        return ExcelFile(workbook, $"thu-chi-{key}.xlsx");
    }

    private static IResult ExportFinanceWorkbook(string? month, DataStore store)
    {
        var key = ResolveMonth(month);
        var payload = store.Read(data => new
        {
            Overview = BuildFinanceOverview(data, key),
            Payrolls = BuildPayrollList(data, key, persist: false),
            Entries = data.Expenses
                .Where(entry => entry.Date.StartsWith(key, StringComparison.Ordinal))
                .OrderByDescending(entry => entry.Date, StringComparer.Ordinal)
                .ThenByDescending(entry => entry.CreatedAt)
                .Select(ApiMappings.ToFinanceEntry)
                .ToList()
        });

        var overview = payload.Overview;
        var workbook = new ExcelWorkbook();
        var summary = workbook.AddSheet(
            "Tổng quan",
            new ExcelColumn("Hạng mục", Width: 34),
            new ExcelColumn("Giá trị (đ)", ExcelColumnKind.Money, 20),
            new ExcelColumn("Ghi chú", ExcelColumnKind.Wrap, 46));
        summary.AddRow("Kỳ báo cáo", key, "");
        summary.AddRow("Doanh thu đơn hàng", overview.OrderRevenue, "Thanh toán đơn hàng trong kỳ");
        summary.AddRow("Doanh thu hợp đồng", overview.ContractRevenue, "Các mốc hợp đồng đã thu");
        summary.AddRow("Thu khác", overview.OtherIncome, "Các khoản thu ghi tay");
        summary.AddTotalRow("Tổng doanh thu", overview.TotalRevenue, "Đơn hàng + hợp đồng + thu khác");
        summary.AddRow("Chi phí", overview.ExpenseTotal, "Chi phí ghi tay trong kỳ");
        summary.AddRow("Lương tháng", overview.PayrollTotal, "Bảng lương tạm tính");
        summary.AddRow("Đã chi lương", overview.PayrollPaid, $"{overview.UnpaidPayrollCount} nhân sự chưa chi");
        summary.AddTotalRow("Lợi nhuận", overview.Profit, "Tổng doanh thu − chi phí − lương");
        summary.AddRow("Công nợ đơn hàng", overview.OutstandingDebt, "");
        summary.AddRow("Công nợ hợp đồng", overview.ContractOutstanding, "");
        summary.AddRow("Lương chưa chi", overview.PayrollTotal - overview.PayrollPaid, "");
        summary.AddRow("Hợp đồng chờ ký", overview.PendingContractCount.ToString(CultureInfo.InvariantCulture), "");

        summary.AddBlankRow();
        summary.AddTotalRow("Cơ cấu chi phí", "", "");
        foreach (var item in overview.ExpenseBreakdown)
        {
            summary.AddRow(item.Category, item.Amount, "");
        }

        summary.AddBlankRow();
        summary.AddTotalRow("Cơ cấu thu nhập", "", "");
        foreach (var item in overview.IncomeBreakdown)
        {
            summary.AddRow(item.Category, item.Amount, "");
        }

        var payroll = workbook.AddSheet(
            "Bảng lương",
            new ExcelColumn("Nhân sự", Width: 22),
            new ExcelColumn("Lương cơ bản", ExcelColumnKind.Money, 16),
            new ExcelColumn("Giờ", ExcelColumnKind.Decimal, 10),
            new ExcelColumn("Doanh thu tính hoa hồng", ExcelColumnKind.Money, 22),
            new ExcelColumn("Hoa hồng", ExcelColumnKind.Money, 16),
            new ExcelColumn("Tổng lương", ExcelColumnKind.Money, 16),
            new ExcelColumn("Trạng thái", Width: 14),
            new ExcelColumn("Ngày chi", Width: 13));
        foreach (var item in payload.Payrolls)
        {
            payroll.AddRow(
                item.StaffName,
                item.Base,
                item.Hours,
                item.CommissionBase,
                item.Commission,
                item.Total,
                item.Paid ? "Đã chi" : "Chưa chi",
                item.PaidAt);
        }

        payroll.AddBlankRow();
        payroll.AddTotalRow(
            "Tổng cộng",
            payload.Payrolls.Sum(item => item.Base),
            payload.Payrolls.Sum(item => item.Hours),
            payload.Payrolls.Sum(item => item.CommissionBase),
            payload.Payrolls.Sum(item => item.Commission),
            payload.Payrolls.Sum(item => item.Total),
            "",
            "");

        AddFinanceEntrySheet(workbook, payload.Entries, "Thu chi");
        return ExcelFile(workbook, $"tai-chinh-luong-{key}.xlsx");
    }

    private static void AddFinanceEntrySheet(
        ExcelWorkbook workbook,
        IReadOnlyList<FinanceEntryDto> entries,
        string sheetName)
    {
        var sheet = workbook.AddSheet(
            sheetName,
            new ExcelColumn("Ngày", Width: 12),
            new ExcelColumn("Loại", Width: 13),
            new ExcelColumn("Danh mục", Width: 24),
            new ExcelColumn("Số tiền (đ)", ExcelColumnKind.Money, 16),
            new ExcelColumn("Ghi chú", ExcelColumnKind.Wrap, 40),
            new ExcelColumn("Ảnh & chú thích", ExcelColumnKind.Wrap, 34),
            new ExcelColumn("Người ghi", Width: 18));
        foreach (var entry in entries)
        {
            var photoNote = string.Join("; ", entry.Photos.Select(photo =>
                string.IsNullOrWhiteSpace(photo.Caption)
                    ? photo.Name
                    : $"{photo.Name}: {photo.Caption}"));
            sheet.AddRow(
                entry.Date,
                FinanceEntryKinds.Label(entry.Kind),
                entry.Category,
                entry.Amount,
                entry.Note,
                photoNote,
                entry.CreatedByName);
        }

        var income = entries
            .Where(entry => entry.Kind == FinanceEntryKinds.Income)
            .Sum(entry => entry.Amount);
        var expense = entries
            .Where(entry => entry.Kind == FinanceEntryKinds.Expense)
            .Sum(entry => entry.Amount);
        sheet.AddBlankRow();
        sheet.AddTotalRow("", "", "Tổng thu", income, "", "", "");
        sheet.AddTotalRow("", "", "Tổng chi", expense, "", "", "");
        sheet.AddTotalRow("", "", "Chênh lệch", income - expense, "", "", "");
    }

    private static IResult ExcelFile(ExcelWorkbook workbook, string fileName) =>
        Results.File(
            workbook.Build(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);

    private static IResult GetExpenses(string? month, DataStore store)
    {
        var payload = store.Read(data =>
        {
            var query = data.Expenses
                .Where(entry => FinanceEntryKinds.Normalize(entry.Kind) == FinanceEntryKinds.Expense);
            if (!string.IsNullOrWhiteSpace(month))
            {
                var key = ResolveMonth(month);
                query = query.Where(entry => entry.Date.StartsWith(key, StringComparison.Ordinal));
            }

            return query
                .OrderByDescending(entry => entry.Date, StringComparer.Ordinal)
                .ThenByDescending(entry => entry.CreatedAt)
                .Select(ApiMappings.ToFinanceEntry)
                .ToList();
        });
        return Results.Ok(payload);
    }

    private static IResult CreateExpense(
        CreateExpenseRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        return CreateFinanceEntry(
            new CreateFinanceEntryRequest
            {
                Kind = FinanceEntryKinds.Expense,
                Date = request.Date,
                Category = request.Category,
                Amount = request.Amount,
                Note = request.Note
            },
            context,
            store,
            sessions);
    }

    private static IResult UpdateExpense(
        string id,
        UpdateExpenseRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        return UpdateFinanceEntry(
            id,
            new UpdateFinanceEntryRequest
            {
                Date = request.Date,
                Category = request.Category,
                Amount = request.Amount,
                Note = request.Note
            },
            context,
            store,
            sessions);
    }

    private static Task<IResult> DeleteExpense(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions,
        IObjectStorage storage)
    {
        return DeleteFinanceEntry(id, context, store, sessions, storage);
    }

    private static IResult GetPayrolls(string? month, DataStore store)
    {
        var key = ResolveMonth(month);
        var payload = store.Read(data =>
            BuildPayrollList(data, key, persist: false));
        return Results.Ok(payload);
    }

    private static IResult ComputePayrolls(
        ComputePayrollRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var key = ResolveMonth(request.Month);
        var payload = store.Write(data =>
        {
            var list = BuildPayrollList(data, key, persist: true);
            StudioAudit.Add(
                data,
                actor,
                "finance",
                "payroll",
                $"đã tính bảng lương tháng {key} cho {list.Count} nhân sự",
                key,
                [$"Tổng lương: {FormatMoney(list.Sum(item => item.Total))}"]);
            return new PayrollComputeDto(key, list.Count, list.Sum(item => item.Total), list);
        });

        return Results.Ok(payload);
    }

    private static IResult TogglePayrollPaid(
        string id,
        TogglePayrollPaidRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var today = Today();
        var dto = store.Write(data =>
        {
            var payroll = FindPayroll(data, id);
            payroll.Paid = request.Paid;
            payroll.PaidAt = request.Paid ? (payroll.PaidAt.Length == 0 ? today : payroll.PaidAt) : "";
            payroll.UpdatedAt = DateTimeOffset.UtcNow;
            var staff = data.Staff.FirstOrDefault(member =>
                string.Equals(member.Id, payroll.StaffId, StringComparison.Ordinal));
            StudioAudit.Add(
                data,
                actor,
                "finance",
                request.Paid ? "payroll-paid" : "payroll-unpaid",
                request.Paid
                    ? $"đã chi lương tháng {payroll.Month} cho {staff?.Name ?? "nhân sự"}"
                    : $"đã bỏ đánh dấu chi lương tháng {payroll.Month}",
                payroll.Id);
            return ApiMappings.ToPayroll(payroll, data);
        });

        return Results.Ok(dto);
    }

    // ------------------------------------------------------------ attendance

    private static IResult GetAttendance(string? date, DataStore store)
    {
        var key = string.IsNullOrWhiteSpace(date) ? Today() : RequireDate(date, "Ngày chấm công");
        var payload = store.Read(data => data.Attendance
            .Where(record => string.Equals(record.Date, key, StringComparison.Ordinal))
            .OrderBy(record => StaffName(data, record.StaffId), StringComparer.OrdinalIgnoreCase)
            .Select(record => ApiMappings.ToAttendance(record, data))
            .ToList());
        return Results.Ok(payload);
    }

    private static IResult GetAttendanceMonth(string? month, DataStore store)
    {
        var key = ResolveMonth(month);
        var payload = store.Read(data =>
        {
            var recordsByStaff = data.Attendance
                .Where(record => record.Date.StartsWith(key, StringComparison.Ordinal))
                .GroupBy(record => record.StaffId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            var staff = data.Staff
                .Where(member => member.IsActive || recordsByStaff.ContainsKey(member.Id))
                .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return staff
                .Select(member => ApiMappings.ToAttendanceMonth(
                    data,
                    key,
                    member,
                    recordsByStaff.GetValueOrDefault(member.Id) ?? []))
                .ToList();
        });
        return Results.Ok(payload);
    }

    private static IResult CreateAttendance(
        CreateAttendanceRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var date = RequireDate(request.Date, "Ngày chấm công");
        var clockIn = NormalizeClock(request.In, "Giờ vào");
        var clockOut = NormalizeClock(request.Out, "Giờ ra");
        var now = DateTimeOffset.UtcNow;

        var dto = store.Write(data =>
        {
            var staff = FindStaff(data, request.StaffId);
            var record = data.Attendance.FirstOrDefault(item =>
                string.Equals(item.StaffId, staff.Id, StringComparison.Ordinal) &&
                string.Equals(item.Date, date, StringComparison.Ordinal));
            if (record is null)
            {
                record = new AttendanceRecord
                {
                    Id = NewShortId(),
                    StaffId = staff.Id,
                    Date = date,
                    In = clockIn,
                    Out = clockOut,
                    Note = AlbumText.TrimTo(request.Note, 500),
                    CreatedAt = now,
                    UpdatedAt = now
                };
                data.Attendance.Add(record);
            }
            else
            {
                if (clockIn.Length > 0)
                {
                    record.In = clockIn;
                }

                if (clockOut.Length > 0)
                {
                    record.Out = clockOut;
                }

                if (request.Note.Length > 0)
                {
                    record.Note = AlbumText.TrimTo(request.Note, 500);
                }

                record.UpdatedAt = now;
            }

            StudioAudit.Add(
                data,
                actor,
                "attendance",
                "save",
                $"đã chấm công {staff.Name} ngày {date} ({record.In}–{record.Out})",
                record.Id);
            return ApiMappings.ToAttendance(record, data);
        });

        return Results.Created($"/api/admin/attendance/{dto.Id}", dto);
    }

    private static IResult UpdateAttendance(
        string id,
        UpdateAttendanceRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var clockIn = request.In is null ? null : NormalizeClock(request.In, "Giờ vào");
        var clockOut = request.Out is null ? null : NormalizeClock(request.Out, "Giờ ra");
        var dto = store.Write(data =>
        {
            var record = FindAttendance(data, id);
            if (clockIn is not null)
            {
                record.In = clockIn;
            }

            if (clockOut is not null)
            {
                record.Out = clockOut;
            }

            if (request.Note is not null)
            {
                record.Note = AlbumText.TrimTo(request.Note, 500);
            }

            record.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "attendance",
                "update",
                $"đã sửa chấm công {StaffName(data, record.StaffId)} ngày {record.Date}",
                record.Id);
            return ApiMappings.ToAttendance(record, data);
        });

        return Results.Ok(dto);
    }

    private static IResult DeleteAttendance(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        store.Write(data =>
        {
            var record = FindAttendance(data, id);
            data.Attendance.Remove(record);
            StudioAudit.Add(
                data,
                actor,
                "attendance",
                "delete",
                $"đã xoá chấm công {StaffName(data, record.StaffId)} ngày {record.Date}",
                record.Id);
            return true;
        });

        return Results.NoContent();
    }

    // ---------------------------------------------------------- shift planner

    private static IResult GetShiftWeek(string? week, DataStore store)
    {
        var start = ResolveWeekStart(week);
        return Results.Ok(store.Read(data => BuildShiftWeek(data, start)));
    }

    private static IResult SaveShift(
        SaveShiftRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var date = RequireDate(request.Date, "Ngày phân ca");
        if (!ShiftAssignmentKinds.IsKnown(request.Shift))
        {
            throw new ApiException(400, "Ca làm việc không hợp lệ.", "invalid_shift");
        }

        var shift = ShiftAssignmentKinds.Normalize(request.Shift);
        var note = AlbumText.TrimTo(request.Note, 300);
        var now = DateTimeOffset.UtcNow;

        var dto = store.Write(data =>
        {
            var staff = FindStaff(data, request.StaffId);
            var assignment = data.ShiftAssignments.FirstOrDefault(item =>
                string.Equals(item.StaffId, staff.Id, StringComparison.Ordinal) &&
                string.Equals(item.Date, date, StringComparison.Ordinal));
            if (assignment is null)
            {
                assignment = new ShiftAssignment
                {
                    Id = NewShortId(),
                    StaffId = staff.Id,
                    Date = date,
                    Shift = shift,
                    Note = note,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                data.ShiftAssignments.Add(assignment);
            }
            else
            {
                assignment.Shift = shift;
                assignment.Note = note;
                assignment.UpdatedAt = now;
            }

            StudioAudit.Add(
                data,
                actor,
                "shift",
                "save",
                $"đã phân ca {staff.Name} ngày {date}: {ShiftAssignmentKinds.Label(shift, staff.Shift)}",
                assignment.Id);
            return ToShiftAssignment(assignment);
        });

        return Results.Ok(dto);
    }

    private static IResult DeleteShift(
        string staffId,
        string date,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var key = RequireDate(date, "Ngày phân ca");
        store.Write(data =>
        {
            var staff = FindStaff(data, staffId);
            var assignment = data.ShiftAssignments.FirstOrDefault(item =>
                string.Equals(item.StaffId, staff.Id, StringComparison.Ordinal) &&
                string.Equals(item.Date, key, StringComparison.Ordinal));
            if (assignment is null)
            {
                throw new ApiException(404, "Không tìm thấy phân ca cần bỏ.", "shift_not_found");
            }

            data.ShiftAssignments.Remove(assignment);
            StudioAudit.Add(
                data,
                actor,
                "shift",
                "delete",
                $"đã bỏ phân ca {staff.Name} ngày {key}",
                assignment.Id);
            return true;
        });

        return Results.NoContent();
    }

    private static IResult FillShiftWeek(
        ShiftWeekRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var start = ResolveWeekStart(request.Week);
        var payload = store.Write(data =>
        {
            var existing = data.ShiftAssignments
                .Where(item => InWeek(item.Date, start))
                .Select(item => item.StaffId + "|" + item.Date)
                .ToHashSet(StringComparer.Ordinal);
            var now = DateTimeOffset.UtcNow;
            var added = 0;
            foreach (var staff in data.Staff.Where(member => member.IsActive))
            {
                for (var index = 0; index < 7; index++)
                {
                    var date = start.AddDays(index).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    if (!existing.Add(staff.Id + "|" + date))
                    {
                        continue;
                    }

                    data.ShiftAssignments.Add(new ShiftAssignment
                    {
                        Id = NewShortId(),
                        StaffId = staff.Id,
                        Date = date,
                        Shift = ShiftAssignmentKinds.Profile,
                        Note = "",
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                    added++;
                }
            }

            StudioAudit.Add(
                data,
                actor,
                "shift",
                "fill",
                $"đã điền ca mặc định tuần {WeekLabel(start)} ({added} ô)",
                "");
            return BuildShiftWeek(data, start);
        });

        return Results.Ok(payload);
    }

    private static IResult ClearShiftWeek(
        string? week,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var start = ResolveWeekStart(week);
        var payload = store.Write(data =>
        {
            var removed = data.ShiftAssignments.RemoveAll(item => InWeek(item.Date, start));
            StudioAudit.Add(
                data,
                actor,
                "shift",
                "clear",
                $"đã xoá lịch chia ca tuần {WeekLabel(start)} ({removed} ô)",
                "");
            return BuildShiftWeek(data, start);
        });

        return Results.Ok(payload);
    }

    private static ShiftWeekDto BuildShiftWeek(AppData data, DateOnly start)
    {
        var end = start.AddDays(6);
        var startKey = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var endKey = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var todayKey = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var assignments = data.ShiftAssignments
            .Where(item => InRange(item.Date, startKey, endKey))
            .ToList();
        var staff = data.Staff
            .Where(member => member.IsActive || assignments.Any(item => item.StaffId == member.Id))
            .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var staffById = staff
            .GroupBy(member => member.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var days = new List<ShiftDayDto>(7);
        for (var index = 0; index < 7; index++)
        {
            var date = start.AddDays(index);
            var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var dayAssignments = assignments.Where(item => item.Date == key).ToList();
            var windows = new List<string>();
            var scheduled = 0;
            var off = 0;
            foreach (var assignment in dayAssignments)
            {
                if (assignment.Shift == ShiftAssignmentKinds.Off)
                {
                    off++;
                    continue;
                }

                scheduled++;
                var profileShift = staffById.GetValueOrDefault(assignment.StaffId)?.Shift;
                var shift = assignment.Shift.Length > 0
                    ? assignment.Shift
                    : StaffShifts.Normalize(profileShift);
                var (windowStart, windowEnd) = StaffShifts.Window(shift);
                var window = $"{windowStart}–{windowEnd}";
                if (!windows.Contains(window, StringComparer.Ordinal))
                {
                    windows.Add(window);
                }
            }

            days.Add(new ShiftDayDto(
                key,
                WeekdayLabel(date.DayOfWeek),
                $"{date.Day:00}/{date.Month:00}",
                string.Join(" · ", windows),
                scheduled,
                off,
                key == todayKey));
        }

        var staffDtos = staff
            .Select(member => new ShiftStaffDto(
                member.Id,
                member.Name,
                member.Role,
                member.Shift,
                member.IsActive,
                assignments
                    .Where(item => item.StaffId == member.Id)
                    .OrderBy(item => item.Date, StringComparer.Ordinal)
                    .Select(ToShiftAssignment)
                    .ToList()))
            .ToList();

        return new ShiftWeekDto(startKey, endKey, WeekLabel(start), days, staffDtos);
    }

    private static ShiftAssignmentDto ToShiftAssignment(ShiftAssignment assignment) =>
        new(assignment.Id, assignment.StaffId, assignment.Date, assignment.Shift, assignment.Note);

    private static DateOnly ResolveWeekStart(string? value)
    {
        var date = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        if (!string.IsNullOrWhiteSpace(value) &&
            DateOnly.TryParseExact(
                value.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            date = parsed;
        }

        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private static bool InWeek(string date, DateOnly start) =>
        InRange(
            date,
            start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            start.AddDays(6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static string WeekLabel(DateOnly start)
    {
        var end = start.AddDays(6);
        return $"{start.Day:00}/{start.Month:00} – {end.Day:00}/{end.Month:00}/{end.Year}";
    }

    private static string WeekdayLabel(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Thứ 2",
        DayOfWeek.Tuesday => "Thứ 3",
        DayOfWeek.Wednesday => "Thứ 4",
        DayOfWeek.Thursday => "Thứ 5",
        DayOfWeek.Friday => "Thứ 6",
        DayOfWeek.Saturday => "Thứ 7",
        _ => "Chủ nhật"
    };

    // -------------------------------------------------------------- wardrobe

    private static IResult GetOutfits(DataStore store)
    {
        var payload = store.Read(data => data.Outfits
            .OrderBy(outfit => outfit.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(outfit => outfit.Name, StringComparer.OrdinalIgnoreCase)
            .Select(outfit => ApiMappings.ToOutfit(outfit, data.OutfitLoans))
            .ToList());
        return Results.Ok(payload);
    }

    private static IResult CreateOutfit(
        CreateOutfitRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var name = RequireName(request.Name, "Tên trang phục");
        var now = DateTimeOffset.UtcNow;
        var dto = store.Write(data =>
        {
            var outfit = new Outfit
            {
                Id = NewShortId(),
                Name = name,
                Category = OutfitCategories.Normalize(request.Category),
                Size = AlbumText.TrimTo(request.Size, 40),
                Color = AlbumText.TrimTo(request.Color, 60),
                Quantity = Math.Clamp(request.Quantity, 0, 10000),
                Note = AlbumText.TrimTo(request.Note, 1000),
                CreatedAt = now,
                UpdatedAt = now
            };
            data.Outfits.Add(outfit);
            StudioAudit.Add(
                data,
                actor,
                "wardrobe",
                "create",
                $"đã thêm trang phục “{outfit.Name}”",
                outfit.Id,
                [$"Danh mục: {outfit.Category}", $"Số lượng: {outfit.Quantity}"]);
            return ApiMappings.ToOutfit(outfit, data.OutfitLoans);
        });

        return Results.Created($"/api/admin/outfits/{dto.Id}", dto);
    }

    private static IResult UpdateOutfit(
        string id,
        UpdateOutfitRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var name = request.Name is null ? null : RequireName(request.Name, "Tên trang phục");
        var dto = store.Write(data =>
        {
            var outfit = FindOutfit(data, id);
            if (name is not null)
            {
                outfit.Name = name;
            }

            if (request.Category is not null)
            {
                outfit.Category = OutfitCategories.Normalize(request.Category);
            }

            if (request.Size is not null)
            {
                outfit.Size = AlbumText.TrimTo(request.Size, 40);
            }

            if (request.Color is not null)
            {
                outfit.Color = AlbumText.TrimTo(request.Color, 60);
            }

            if (request.Quantity is not null)
            {
                outfit.Quantity = Math.Clamp(request.Quantity.Value, 0, 10000);
            }

            if (request.Note is not null)
            {
                outfit.Note = AlbumText.TrimTo(request.Note, 1000);
            }

            outfit.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "wardrobe",
                "update",
                $"đã cập nhật trang phục “{outfit.Name}”",
                outfit.Id);
            return ApiMappings.ToOutfit(outfit, data.OutfitLoans);
        });

        return Results.Ok(dto);
    }

    private static IResult DeleteOutfit(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        store.Write(data =>
        {
            var outfit = FindOutfit(data, id);
            if (data.OutfitLoans.Any(loan =>
                    string.Equals(loan.OutfitId, outfit.Id, StringComparison.Ordinal)))
            {
                throw new ApiException(
                    409,
                    "Trang phục đã có lịch sử cho thuê. Hãy xoá các phiếu cho thuê trước.",
                    "outfit_in_use");
            }

            data.Outfits.Remove(outfit);
            StudioAudit.Add(
                data,
                actor,
                "wardrobe",
                "delete",
                $"đã xoá trang phục “{outfit.Name}”",
                outfit.Id);
            return true;
        });

        return Results.NoContent();
    }

    private static IResult GetConcepts(DataStore store)
    {
        var payload = store.Read(data => data.Concepts
            .OrderBy(concept => concept.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ApiMappings.ToConcept)
            .ToList());
        return Results.Ok(payload);
    }

    private static IResult CreateConcept(
        CreateConceptRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var name = RequireName(request.Name, "Tên concept");
        var now = DateTimeOffset.UtcNow;
        var dto = store.Write(data =>
        {
            var concept = new StudioConcept
            {
                Id = NewShortId(),
                Name = name,
                Description = AlbumText.TrimTo(request.Description, 2000),
                Fee = Math.Max(0, ApiMappings.RoundMoney(request.Fee)),
                Status = ConceptStatuses.Normalize(request.Status),
                CreatedAt = now,
                UpdatedAt = now
            };
            data.Concepts.Add(concept);
            StudioAudit.Add(
                data,
                actor,
                "wardrobe",
                "concept-create",
                $"đã thêm concept “{concept.Name}”",
                concept.Id);
            return ApiMappings.ToConcept(concept);
        });

        return Results.Created($"/api/admin/concepts/{dto.Id}", dto);
    }

    private static IResult UpdateConcept(
        string id,
        UpdateConceptRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var name = request.Name is null ? null : RequireName(request.Name, "Tên concept");
        var dto = store.Write(data =>
        {
            var concept = FindConcept(data, id);
            if (name is not null)
            {
                concept.Name = name;
            }

            if (request.Description is not null)
            {
                concept.Description = AlbumText.TrimTo(request.Description, 2000);
            }

            if (request.Fee is not null)
            {
                concept.Fee = Math.Max(0, ApiMappings.RoundMoney(request.Fee.Value));
            }

            if (request.Status is not null)
            {
                concept.Status = ConceptStatuses.Normalize(request.Status);
            }

            concept.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "wardrobe",
                "concept-update",
                $"đã cập nhật concept “{concept.Name}”",
                concept.Id);
            return ApiMappings.ToConcept(concept);
        });

        return Results.Ok(dto);
    }

    private static IResult DeleteConcept(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        store.Write(data =>
        {
            var concept = FindConcept(data, id);
            data.Concepts.Remove(concept);
            StudioAudit.Add(
                data,
                actor,
                "wardrobe",
                "concept-delete",
                $"đã xoá concept “{concept.Name}”",
                concept.Id);
            return true;
        });

        return Results.NoContent();
    }

    private static IResult GetLoans(string? status, DataStore store)
    {
        var today = Today();
        var payload = store.Read(data =>
        {
            var query = data.OutfitLoans.AsEnumerable();
            if (string.Equals(status, "open", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(loan => string.IsNullOrWhiteSpace(loan.ReturnDate));
            }
            else if (string.Equals(status, "returned", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(loan => !string.IsNullOrWhiteSpace(loan.ReturnDate));
            }

            return query
                .OrderByDescending(loan => loan.BorrowDate, StringComparer.Ordinal)
                .ThenByDescending(loan => loan.CreatedAt)
                .Select(loan => ApiMappings.ToLoan(loan, data, today))
                .ToList();
        });
        return Results.Ok(payload);
    }

    private static IResult CreateLoan(
        CreateLoanRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var borrowDate = RequireDate(request.BorrowDate, "Ngày cho thuê");
        var dueDate = string.IsNullOrWhiteSpace(request.DueDate)
            ? ""
            : RequireDate(request.DueDate, "Hạn trả");
        var now = DateTimeOffset.UtcNow;

        var dto = store.Write(data =>
        {
            var outfit = FindOutfit(data, request.OutfitId);
            var quantity = Math.Clamp(request.Quantity, 1, 10000);
            EnsureAvailable(data, outfit, quantity, null);

            var loan = new OutfitLoan
            {
                Id = NewShortId(),
                OutfitId = outfit.Id,
                Quantity = quantity,
                BorrowerType = LoanBorrowerTypes.Normalize(request.BorrowerType),
                CustomerId = ResolveCustomerId(data, request.CustomerId),
                StaffId = ResolveStaffId(data, request.StaffId),
                BookingId = ResolveBookingId(data, request.BookingId),
                BorrowDate = borrowDate,
                DueDate = dueDate,
                Deposit = Math.Max(0, ApiMappings.RoundMoney(request.Deposit)),
                Notes = AlbumText.TrimTo(request.Notes, 1000),
                CreatedAt = now,
                UpdatedAt = now
            };

            if (loan.BorrowerType == LoanBorrowerTypes.Staff && loan.StaffId.Length == 0)
            {
                loan.BorrowerType = LoanBorrowerTypes.Customer;
            }

            data.OutfitLoans.Add(loan);
            StudioAudit.Add(
                data,
                actor,
                "wardrobe",
                "loan",
                $"đã cho thuê “{outfit.Name}” × {quantity}",
                loan.Id,
                [BorrowerLabel(data, loan), dueDate.Length == 0 ? "" : $"Hạn trả: {dueDate}"]);
            StudioNotifications.Push(
                data,
                NotificationTypes.Wardrobe,
                $"Cho thuê “{outfit.Name}”",
                $"{BorrowerLabel(data, loan)} mượn {quantity} món" +
                (dueDate.Length == 0 ? "." : $", hạn trả {dueDate}."),
                "/admin/wardrobe");
            return ApiMappings.ToLoan(loan, data, Today());
        });

        return Results.Created($"/api/admin/loans/{dto.Id}", dto);
    }

    private static IResult UpdateLoan(
        string id,
        UpdateLoanRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var borrowDate = request.BorrowDate is null ? null : RequireDate(request.BorrowDate, "Ngày cho thuê");
        var dueDate = request.DueDate is null
            ? null
            : string.IsNullOrWhiteSpace(request.DueDate) ? "" : RequireDate(request.DueDate, "Hạn trả");

        var dto = store.Write(data =>
        {
            var loan = FindLoan(data, id);
            if (request.OutfitId is not null)
            {
                var outfit = FindOutfit(data, request.OutfitId);
                loan.OutfitId = outfit.Id;
            }

            if (request.Quantity is not null)
            {
                var outfit = FindOutfit(data, loan.OutfitId);
                var quantity = Math.Clamp(request.Quantity.Value, 1, 10000);
                EnsureAvailable(data, outfit, quantity, loan.Id);
                loan.Quantity = quantity;
            }

            if (request.BorrowerType is not null)
            {
                loan.BorrowerType = LoanBorrowerTypes.Normalize(request.BorrowerType);
            }

            if (request.CustomerId is not null)
            {
                loan.CustomerId = ResolveCustomerId(data, request.CustomerId);
            }

            if (request.StaffId is not null)
            {
                loan.StaffId = ResolveStaffId(data, request.StaffId);
            }

            if (request.BookingId is not null)
            {
                loan.BookingId = ResolveBookingId(data, request.BookingId);
            }

            if (borrowDate is not null)
            {
                loan.BorrowDate = borrowDate;
            }

            if (dueDate is not null)
            {
                loan.DueDate = dueDate;
            }

            if (request.Deposit is not null)
            {
                loan.Deposit = Math.Max(0, ApiMappings.RoundMoney(request.Deposit.Value));
            }

            if (request.Notes is not null)
            {
                loan.Notes = AlbumText.TrimTo(request.Notes, 1000);
            }

            loan.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "wardrobe",
                "loan-update",
                $"đã cập nhật phiếu cho thuê “{OutfitName(data, loan.OutfitId)}”",
                loan.Id);
            return ApiMappings.ToLoan(loan, data, Today());
        });

        return Results.Ok(dto);
    }

    private static IResult ReturnLoan(
        string id,
        ReturnLoanRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var returnDate = string.IsNullOrWhiteSpace(request.ReturnDate)
            ? Today()
            : RequireDate(request.ReturnDate, "Ngày trả");
        var dto = store.Write(data =>
        {
            var loan = FindLoan(data, id);
            if (!string.IsNullOrWhiteSpace(loan.ReturnDate))
            {
                throw new ApiException(409, "Phiếu cho thuê này đã được đánh dấu trả.", "loan_returned");
            }

            loan.ReturnDate = returnDate;
            loan.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "wardrobe",
                "loan-return",
                $"đã nhận lại “{OutfitName(data, loan.OutfitId)}” × {loan.Quantity}",
                loan.Id);
            StudioNotifications.Push(
                data,
                NotificationTypes.Wardrobe,
                $"Đã nhận lại “{OutfitName(data, loan.OutfitId)}”",
                $"{BorrowerLabel(data, loan)} đã trả {loan.Quantity} món ngày {returnDate}.",
                "/admin/wardrobe");
            return ApiMappings.ToLoan(loan, data, Today());
        });

        return Results.Ok(dto);
    }

    private static IResult DeleteLoan(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        store.Write(data =>
        {
            var loan = FindLoan(data, id);
            data.OutfitLoans.Remove(loan);
            StudioAudit.Add(
                data,
                actor,
                "wardrobe",
                "loan-delete",
                $"đã xoá phiếu cho thuê “{OutfitName(data, loan.OutfitId)}”",
                loan.Id);
            return true;
        });

        return Results.NoContent();
    }

    // --------------------------------------------------------- notifications

    private static IResult GetNotifications(DataStore store)
    {
        var today = Today();
        var needsScan = store.Read(data =>
            !string.Equals(data.LastOverdueScan, today, StringComparison.Ordinal));
        if (needsScan)
        {
            store.Write(data =>
            {
                StudioNotifications.ScanOverdue(data, today);
                data.LastOverdueScan = today;
                return true;
            });
        }

        var payload = store.Read(data => data.Notifications
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(100)
            .Select(ApiMappings.ToNotification)
            .ToList());
        return Results.Ok(payload);
    }

    private static IResult MarkNotificationRead(
        string id,
        DataStore store)
    {
        var dto = store.Write(data =>
        {
            var notification = FindNotification(data, id);
            notification.IsRead = true;
            return ApiMappings.ToNotification(notification);
        });
        return Results.Ok(dto);
    }

    private static IResult MarkAllNotificationsRead(DataStore store)
    {
        var count = store.Write(data =>
        {
            var unread = data.Notifications.Where(notification => !notification.IsRead).ToList();
            foreach (var notification in unread)
            {
                notification.IsRead = true;
            }

            return unread.Count;
        });
        return Results.Ok(new { updated = count });
    }

    private static IResult DeleteNotification(string id, DataStore store)
    {
        store.Write(data =>
        {
            var notification = FindNotification(data, id);
            data.Notifications.Remove(notification);
            return true;
        });
        return Results.NoContent();
    }

    private static IResult GetNotificationSettings(DataStore store)
    {
        var payload = store.Read(data =>
            ApiMappings.ToNotificationSettings(data.NotificationSettings));
        return Results.Ok(payload);
    }

    private static IResult UpdateNotificationSettings(
        NotificationSettingsRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var dto = store.Write(data =>
        {
            data.NotificationSettings = new NotificationSettings
            {
                Master = request.Master,
                Booking = request.Booking,
                Payment = request.Payment,
                Contract = request.Contract,
                Wardrobe = request.Wardrobe,
                Payroll = request.Payroll
            };
            StudioAudit.Add(
                data,
                actor,
                "notifications",
                "settings",
                "đã cập nhật cài đặt thông báo",
                "notifications",
                [$"Công tắc tổng: {(request.Master ? "bật" : "tắt")}"]);
            return ApiMappings.ToNotificationSettings(data.NotificationSettings);
        });
        return Results.Ok(dto);
    }

    // ------------------------------------------------------------ finance calc

    private static FinanceOverviewDto BuildFinanceOverview(AppData data, string month)
    {
        var (from, to) = MonthRange(month);
        var orderRevenue = OrderRevenue(data, from, to);
        var contractRevenue = ContractRevenue(data, from, to);
        var entries = data.Expenses
            .Where(entry => InRange(entry.Date, from, to))
            .ToList();
        var incomeEntries = entries
            .Where(entry => FinanceEntryKinds.Normalize(entry.Kind) == FinanceEntryKinds.Income)
            .ToList();
        var expenses = entries
            .Where(entry => FinanceEntryKinds.Normalize(entry.Kind) == FinanceEntryKinds.Expense)
            .ToList();
        var otherIncome = ApiMappings.RoundMoney(incomeEntries.Sum(entry => entry.Amount));
        var totalRevenue = ApiMappings.RoundMoney(orderRevenue + contractRevenue + otherIncome);
        var expenseTotal = ApiMappings.RoundMoney(expenses.Sum(expense => expense.Amount));
        var payroll = BuildPayrollEntries(data, month);
        var payrollTotal = ApiMappings.RoundMoney(payroll.Sum(item => item.Total));
        var payrollPaid = ApiMappings.RoundMoney(payroll.Where(item => item.Paid).Sum(item => item.Total));
        var openOrders = data.Orders
            .Where(order => !string.Equals(order.Status, OrderStatuses.Cancelled, StringComparison.Ordinal))
            .ToList();
        var openContracts = data.Contracts
            .Where(contract => !string.Equals(contract.Status, ContractStatuses.Cancelled, StringComparison.Ordinal))
            .ToList();

        return new FinanceOverviewDto(
            month,
            orderRevenue,
            contractRevenue,
            otherIncome,
            totalRevenue,
            expenseTotal,
            payrollTotal,
            payrollPaid,
            ApiMappings.RoundMoney(totalRevenue - expenseTotal - payrollTotal),
            ApiMappings.RoundMoney(openOrders.Sum(order => OrderRemaining(order, data))),
            ApiMappings.RoundMoney(openContracts.Sum(ContractRemaining)),
            payroll.Count(item => !item.Paid),
            openContracts.Count(contract =>
                contract.Status is ContractStatuses.Draft or ContractStatuses.Pending),
            expenses
                .GroupBy(expense => expense.Category, StringComparer.Ordinal)
                .Select(group => new FinanceCategoryDto(
                    group.Key,
                    ApiMappings.RoundMoney(group.Sum(expense => expense.Amount))))
                .OrderByDescending(item => item.Amount)
                .ToList(),
            BuildIncomeBreakdown(orderRevenue, contractRevenue, incomeEntries),
            payroll
                .OrderBy(item => StaffName(data, item.StaffId), StringComparer.OrdinalIgnoreCase)
                .Select(item => new FinanceStaffDto(
                    item.StaffId,
                    StaffName(data, item.StaffId),
                    item.Base,
                    item.Hours,
                    item.Commission,
                    item.Total,
                    item.Paid))
                .ToList(),
            entries
                .OrderByDescending(entry => entry.Date, StringComparer.Ordinal)
                .ThenByDescending(entry => entry.CreatedAt)
                .Take(10)
                .Select(ApiMappings.ToFinanceEntry)
                .ToList());
    }

    private static List<FinanceCategoryDto> BuildIncomeBreakdown(
        decimal orderRevenue,
        decimal contractRevenue,
        IReadOnlyList<ExpenseEntry> incomeEntries)
    {
        var items = new List<FinanceCategoryDto>();
        if (orderRevenue > 0)
        {
            items.Add(new FinanceCategoryDto("Đơn hàng", orderRevenue));
        }

        if (contractRevenue > 0)
        {
            items.Add(new FinanceCategoryDto("Hợp đồng", contractRevenue));
        }

        items.AddRange(incomeEntries
            .GroupBy(entry => entry.Category, StringComparer.Ordinal)
            .Select(group => new FinanceCategoryDto(
                group.Key,
                ApiMappings.RoundMoney(group.Sum(entry => entry.Amount)))));

        return items
            .OrderByDescending(item => item.Amount)
            .ToList();
    }

    private static List<PayrollDto> BuildPayrollList(AppData data, string month, bool persist)
    {
        var entries = BuildPayrollEntries(data, month);
        if (persist)
        {
            foreach (var entry in entries)
            {
                var existing = data.Payrolls.FirstOrDefault(payroll =>
                    string.Equals(payroll.StaffId, entry.StaffId, StringComparison.Ordinal) &&
                    string.Equals(payroll.Month, entry.Month, StringComparison.Ordinal));
                if (existing is null)
                {
                    data.Payrolls.Add(entry);
                }
                else if (!existing.Paid)
                {
                    existing.Base = entry.Base;
                    existing.Hours = entry.Hours;
                    existing.CommissionBase = entry.CommissionBase;
                    existing.Commission = entry.Commission;
                    existing.Total = entry.Total;
                    existing.ComputedAt = entry.ComputedAt;
                    existing.UpdatedAt = entry.UpdatedAt;
                }
            }
        }

        return entries
            .OrderBy(entry => StaffName(data, entry.StaffId), StringComparer.OrdinalIgnoreCase)
            .Select(entry => ApiMappings.ToPayroll(entry, data))
            .ToList();
    }

    private static List<PayrollEntry> BuildPayrollEntries(AppData data, string month)
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new List<PayrollEntry>();
        foreach (var staff in data.Staff.Where(member => member.IsActive))
        {
            var existing = data.Payrolls.FirstOrDefault(payroll =>
                string.Equals(payroll.StaffId, staff.Id, StringComparison.Ordinal) &&
                string.Equals(payroll.Month, month, StringComparison.Ordinal));
            if (existing is not null && existing.Paid)
            {
                entries.Add(existing);
                continue;
            }

            var hours = ApiMappings.RoundMoney(data.Attendance
                .Where(record => string.Equals(record.StaffId, staff.Id, StringComparison.Ordinal) &&
                                 record.Date.StartsWith(month, StringComparison.Ordinal))
                .Sum(OperationsMath.WorkedHours));
            var basePay = PayModes.Normalize(staff.PayMode) == PayModes.Hourly
                ? ApiMappings.RoundMoney(hours * staff.HourlyRate)
                : ApiMappings.RoundMoney(staff.BaseSalary);
            var commissionBase = ApiMappings.RoundMoney(CommissionBase(data, staff.Id, month));
            var commission = ApiMappings.RoundMoney(commissionBase * staff.CommissionRate / 100m);
            entries.Add(new PayrollEntry
            {
                Id = existing?.Id ?? NewShortId(),
                Month = month,
                StaffId = staff.Id,
                Base = basePay,
                Hours = hours,
                CommissionBase = commissionBase,
                Commission = commission,
                Total = ApiMappings.RoundMoney(basePay + commission),
                Paid = false,
                PaidAt = "",
                ComputedAt = existing?.ComputedAt ?? now,
                UpdatedAt = now
            });
        }

        // Keep payroll rows of staff who are no longer active so history is not lost.
        foreach (var payroll in data.Payrolls.Where(payroll =>
                     string.Equals(payroll.Month, month, StringComparison.Ordinal) &&
                     !entries.Any(entry => string.Equals(entry.StaffId, payroll.StaffId, StringComparison.Ordinal))))
        {
            entries.Add(payroll);
        }

        return entries;
    }

    private static decimal CommissionBase(AppData data, string staffId, string month)
    {
        var bookingIds = data.Bookings
            .Where(booking => string.Equals(booking.PhotographerId, staffId, StringComparison.Ordinal) &&
                              booking.Date.StartsWith(month, StringComparison.Ordinal))
            .Select(booking => booking.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (bookingIds.Count == 0)
        {
            return 0;
        }

        return data.Orders
            .Where(order => !string.Equals(order.Status, OrderStatuses.Cancelled, StringComparison.Ordinal) &&
                            order.BookingId.Length > 0 &&
                            bookingIds.Contains(order.BookingId))
            .Sum(OrderTotal);
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

    // --------------------------------------------------------------- helpers

    private static string ResolveMonth(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Today()[..7];
        }

        var normalized = NormalizeMonth(value);
        if (normalized.Length == 0)
        {
            throw new ApiException(400, "Tháng không hợp lệ (định dạng YYYY-MM).", "invalid_month");
        }

        return normalized;
    }

    private static string NormalizeMonth(string? value)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0)
        {
            return "";
        }

        if (DateTime.TryParseExact(
                trimmed + "-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var month))
        {
            return month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        return "";
    }

    private static (string From, string To) MonthRange(string month)
    {
        if (!DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var start))
        {
            throw new ApiException(400, "Tháng không hợp lệ.", "invalid_month");
        }

        var end = start.AddMonths(1).AddDays(-1);
        return (
            start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    private static string RequireDate(string? value, string label)
    {
        var normalized = AlbumText.NormalizeEventDate(value);
        if (normalized.Length == 0)
        {
            throw new ApiException(400, $"{label} không hợp lệ (định dạng YYYY-MM-DD).", "invalid_date");
        }

        return normalized;
    }

    private static string NormalizeClock(string? value, string label)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0)
        {
            return "";
        }

        if (!TimeOnly.TryParseExact(
                trimmed,
                ["HH:mm", "H:mm"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var time))
        {
            throw new ApiException(400, $"{label} không hợp lệ (định dạng HH:mm).", "invalid_time");
        }

        return time.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private static string RequireName(string? value, string label)
    {
        var name = AlbumText.TrimTo(value, 120);
        if (name.Length == 0)
        {
            throw new ApiException(400, $"{label} không được để trống.", "invalid_name");
        }

        return name;
    }

    private static bool InRange(string date, string from, string to) =>
        date.Length >= 10 &&
        string.CompareOrdinal(date[..10], from) >= 0 &&
        string.CompareOrdinal(date[..10], to) <= 0;

    private static void EnsureAvailable(AppData data, Outfit outfit, int quantity, string? excludeLoanId)
    {
        var loaned = data.OutfitLoans
            .Where(loan => string.Equals(loan.OutfitId, outfit.Id, StringComparison.Ordinal) &&
                           string.IsNullOrWhiteSpace(loan.ReturnDate) &&
                           !string.Equals(loan.Id, excludeLoanId, StringComparison.Ordinal))
            .Sum(loan => loan.Quantity);
        var available = Math.Max(0, outfit.Quantity - loaned);
        if (quantity > available)
        {
            throw new ApiException(
                409,
                $"“{outfit.Name}” chỉ còn {available} món chưa cho thuê.",
                "outfit_unavailable");
        }
    }

    private static string ResolveCustomerId(AppData data, string? customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return "";
        }

        var customer = data.Customers.FirstOrDefault(item =>
            string.Equals(item.Id, customerId, StringComparison.Ordinal));
        if (customer is null)
        {
            throw new ApiException(400, "Không tìm thấy khách hàng.", "customer_not_found");
        }

        return customer.Id;
    }

    private static string ResolveStaffId(AppData data, string? staffId)
    {
        if (string.IsNullOrWhiteSpace(staffId))
        {
            return "";
        }

        var staff = data.Staff.FirstOrDefault(item =>
            string.Equals(item.Id, staffId, StringComparison.Ordinal));
        if (staff is null)
        {
            throw new ApiException(400, "Không tìm thấy nhân sự.", "staff_not_found");
        }

        return staff.Id;
    }

    private static string ResolveBookingId(AppData data, string? bookingId)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
        {
            return "";
        }

        var booking = data.Bookings.FirstOrDefault(item =>
            string.Equals(item.Id, bookingId, StringComparison.Ordinal));
        if (booking is null)
        {
            throw new ApiException(400, "Không tìm thấy lịch chụp.", "booking_not_found");
        }

        return booking.Id;
    }

    private static string BorrowerLabel(AppData data, OutfitLoan loan)
    {
        if (loan.BorrowerType == LoanBorrowerTypes.Staff)
        {
            return StaffName(data, loan.StaffId);
        }

        var customer = data.Customers.FirstOrDefault(item =>
            string.Equals(item.Id, loan.CustomerId, StringComparison.Ordinal));
        return customer?.Name ?? "Khách lẻ";
    }

    private static string OutfitName(AppData data, string outfitId)
    {
        var outfit = data.Outfits.FirstOrDefault(item =>
            string.Equals(item.Id, outfitId, StringComparison.Ordinal));
        return outfit?.Name ?? "Trang phục";
    }

    private static string StaffName(AppData data, string staffId)
    {
        var staff = data.Staff.FirstOrDefault(member =>
            string.Equals(member.Id, staffId, StringComparison.Ordinal));
        return staff?.Name ?? "Nhân sự";
    }

    private static string ActorName(StudioUser actor) =>
        string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.Username : actor.DisplayName;

    private static StaffMember FindStaff(AppData data, string id) =>
        data.Staff.FirstOrDefault(member => string.Equals(member.Id, id, StringComparison.Ordinal))
        ?? throw new ApiException(404, "Không tìm thấy nhân sự.", "staff_not_found");

    private static ExpenseEntry FindFinanceEntry(AppData data, string id) =>
        data.Expenses.FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.Ordinal))
        ?? throw new ApiException(404, "Không tìm thấy khoản thu chi.", "finance_entry_not_found");

    private static string ParseFinanceKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return FinanceEntryKinds.Expense;
        }

        if (!FinanceEntryKinds.IsKnown(value))
        {
            throw new ApiException(400, "Loại thu chi không hợp lệ.", "invalid_finance_kind");
        }

        return FinanceEntryKinds.Normalize(value);
    }

    private static string NormalizeFinanceCategory(string kind, string? category) =>
        kind == FinanceEntryKinds.Income
            ? IncomeCategories.Normalize(category)
            : ExpenseCategories.Normalize(category);

    private static async Task DeleteFinancePhotoFile(
        IObjectStorage storage,
        string? fileName,
        CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(fileName ?? "");
        if (name.Length == 0)
        {
            return;
        }

        try
        {
            await storage.DeleteAsync(name, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or Amazon.S3.AmazonS3Exception)
        {
            // Ảnh mồ côi không chặn thao tác xoá khoản thu chi.
        }
    }

    private static bool LooksLikeImage(byte[] bytes, string extension)
    {
        if (bytes.Length < 12)
        {
            return false;
        }

        return extension switch
        {
            ".jpg" or ".jpeg" => bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            ".png" => bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,
            ".webp" => bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                       bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50,
            _ => false
        };
    }

    private static readonly Dictionary<string, string> FinancePhotoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private const int MaxFinancePhotosPerEntry = 6;
    private const long MaxFinancePhotoBytes = 5 * 1024 * 1024;

    private static PayrollEntry FindPayroll(AppData data, string id) =>
        data.Payrolls.FirstOrDefault(payroll => string.Equals(payroll.Id, id, StringComparison.Ordinal))
        ?? throw new ApiException(404, "Không tìm thấy bảng lương.", "payroll_not_found");

    private static AttendanceRecord FindAttendance(AppData data, string id) =>
        data.Attendance.FirstOrDefault(record => string.Equals(record.Id, id, StringComparison.Ordinal))
        ?? throw new ApiException(404, "Không tìm thấy bản ghi chấm công.", "attendance_not_found");

    private static Outfit FindOutfit(AppData data, string id) =>
        data.Outfits.FirstOrDefault(outfit => string.Equals(outfit.Id, id, StringComparison.Ordinal))
        ?? throw new ApiException(404, "Không tìm thấy trang phục.", "outfit_not_found");

    private static StudioConcept FindConcept(AppData data, string id) =>
        data.Concepts.FirstOrDefault(concept => string.Equals(concept.Id, id, StringComparison.Ordinal))
        ?? throw new ApiException(404, "Không tìm thấy concept.", "concept_not_found");

    private static OutfitLoan FindLoan(AppData data, string id) =>
        data.OutfitLoans.FirstOrDefault(loan => string.Equals(loan.Id, id, StringComparison.Ordinal))
        ?? throw new ApiException(404, "Không tìm thấy phiếu cho thuê.", "loan_not_found");

    private static NotificationEntry FindNotification(AppData data, string id) =>
        data.Notifications.FirstOrDefault(notification =>
            string.Equals(notification.Id, id, StringComparison.Ordinal))
        ?? throw new ApiException(404, "Không tìm thấy thông báo.", "notification_not_found");

    private static string Today() =>
        DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7))
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatMoney(decimal value) =>
        string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0} ₫", value);

    private static string NewShortId() => Guid.NewGuid().ToString("N")[..12];
}
