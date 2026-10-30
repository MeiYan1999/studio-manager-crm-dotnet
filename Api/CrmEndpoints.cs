using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;
using StudioCore.Models;
using StudioCore.Services;

namespace StudioCore.Api;

/// <summary>
/// Phase 1 CRM: khách hàng, gói dịch vụ, nhân sự, lịch chụp và nhật ký studio.
/// Dữ liệu dùng chung kho JSON với album và bảng công việc.
/// </summary>
public static class CrmEndpoints
{
    private static readonly JsonSerializerOptions ImportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly (string Key, string Label)[] UnsupportedCollections =
    [
        ("tasks", "công việc CRM"),
        ("taskColumns", "cột công việc CRM"),
        ("users", "tài khoản CRM"),
        ("logs", "nhật ký CRM"),
        ("branches", "chi nhánh"),
        ("chats", "hội thoại"),
        ("msgTemplates", "mẫu tin nhắn"),
        ("cash", "sổ quỹ"),
        ("departments", "phòng ban"),
        ("roleGroups", "nhóm quyền")
    ];

    public static void Map(RouteGroupBuilder group)
    {
        var customers = new StudioPermissionFilter(StudioPermissions.Customers);
        var customersRead = new StudioAnyPermissionFilter(
            StudioPermissions.Customers,
            StudioPermissions.Bookings,
            StudioPermissions.Orders,
            StudioPermissions.Contracts,
            StudioPermissions.Wardrobe);
        var packages = new StudioPermissionFilter(StudioPermissions.Packages);
        var packagesRead = new StudioAnyPermissionFilter(
            StudioPermissions.Packages,
            StudioPermissions.Bookings,
            StudioPermissions.Orders,
            StudioPermissions.Contracts);
        var staff = new StudioPermissionFilter(StudioPermissions.Staff);
        var staffRead = new StudioAnyPermissionFilter(
            StudioPermissions.Staff,
            StudioPermissions.Bookings,
            StudioPermissions.Attendance,
            StudioPermissions.Wardrobe);
        var bookings = new StudioPermissionFilter(StudioPermissions.Bookings);
        var bookingsRead = new StudioAnyPermissionFilter(
            StudioPermissions.Bookings,
            StudioPermissions.Orders,
            StudioPermissions.Contracts,
            StudioPermissions.Wardrobe);
        var audit = new StudioPermissionFilter(StudioPermissions.Audit);
        var summary = new StudioAnyPermissionFilter(
            StudioPermissions.Dashboard,
            StudioPermissions.Customers,
            StudioPermissions.Bookings);

        group.MapGet("/crm/summary", GetSummary).AddEndpointFilter(summary);
        group.MapGet("/crm/audit", GetAudit).AddEndpointFilter(audit);
        group.MapPost("/crm/import", ImportBackup).AddEndpointFilter<StudioAdminFilter>();

        group.MapGet("/customers", GetCustomers).AddEndpointFilter(customersRead);
        group.MapGet("/customers/{id}", GetCustomer).AddEndpointFilter(customersRead);
        group.MapPost("/customers", CreateCustomer).AddEndpointFilter(customers);
        group.MapPut("/customers/{id}", UpdateCustomer).AddEndpointFilter(customers);
        group.MapDelete("/customers/{id}", DeleteCustomer).AddEndpointFilter(customers);

        group.MapGet("/price-catalog", (DataStore store) => Results.Ok(store.Read(data => data.PriceCatalog))).AddEndpointFilter(packages);
        group.MapPost("/price-catalog/groups", (PriceGroup request, HttpContext context, DataStore store, AdminSessionStore sessions) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 120 || (request.Description?.Length ?? 0) > 500)
                throw new ApiException(400, "Tên nhóm hoặc mô tả chưa hợp lệ.", "invalid_price_group");
            var actor = sessions.RequireUser(context);
            var groupItem = store.Write(data => {
                var created = new PriceGroup { Id = NewShortId(), Title = request.Title.Trim(), Description = (request.Description ?? "").Trim() };
                data.PriceCatalog.Add(created);
                StudioAudit.Add(data, actor, "packages", "create", $"đã thêm nhóm bảng giá “{created.Title}”", created.Id, []);
                return created;
            });
            return Results.Created($"/api/admin/price-catalog/{groupItem.Id}", groupItem);
        }).AddEndpointFilter(packages);
        group.MapPut("/price-catalog/groups/{groupId}", (string groupId, PriceGroup request, HttpContext context, DataStore store, AdminSessionStore sessions) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 120 || (request.Description?.Length ?? 0) > 500)
                throw new ApiException(400, "Tên nhóm hoặc mô tả chưa hợp lệ.", "invalid_price_group");
            var actor = sessions.RequireUser(context);
            return Results.Ok(store.Write(data => {
                var found = data.PriceCatalog.FirstOrDefault(g => g.Id == groupId)
                    ?? throw new ApiException(404, "Không tìm thấy nhóm bảng giá.", "price_group_not_found");
                found.Title = request.Title.Trim(); found.Description = (request.Description ?? "").Trim();
                StudioAudit.Add(data, actor, "packages", "update", $"đã cập nhật nhóm bảng giá “{found.Title}”", found.Id, []);
                return found;
            }));
        }).AddEndpointFilter(packages);
        group.MapDelete("/price-catalog/groups/{groupId}", (string groupId, int expectedCount, HttpContext context, DataStore store, AdminSessionStore sessions) =>
        {
            var actor = sessions.RequireUser(context);
            store.Write(data => {
                var found = data.PriceCatalog.FirstOrDefault(g => g.Id == groupId)
                    ?? throw new ApiException(404, "Không tìm thấy nhóm bảng giá.", "price_group_not_found");
                if (found.Items.Count != expectedCount)
                    throw new ApiException(409, "Số gói trong nhóm đã thay đổi. Hãy tải lại và kiểm tra trước khi xóa.", "price_group_changed");
                data.PriceCatalog.Remove(found);
                StudioAudit.Add(data, actor, "packages", "delete", $"đã xóa nhóm bảng giá “{found.Title}”", found.Id, [$"{expectedCount} gói"]);
            });
            return Results.NoContent();
        }).AddEndpointFilter(packages);
        group.MapDelete("/price-catalog/{groupId}/{itemId}", (string groupId, string itemId, HttpContext context, DataStore store, AdminSessionStore sessions) =>
        {
            var actor = sessions.RequireUser(context);
            store.Write(data => {
                var parent = data.PriceCatalog.FirstOrDefault(g => g.Id == groupId)
                    ?? throw new ApiException(404, "Không tìm thấy nhóm bảng giá.", "price_group_not_found");
                var found = parent.Items.FirstOrDefault(i => i.Id == itemId)
                    ?? throw new ApiException(404, "Không tìm thấy gói trong bảng giá.", "price_item_not_found");
                parent.Items.Remove(found);
                StudioAudit.Add(data, actor, "packages", "delete", $"đã xóa gói bảng giá “{found.Name}”", found.Id, []);
            });
            return Results.NoContent();
        }).AddEndpointFilter(packages);
        group.MapPost("/price-catalog/{groupId}/items", (string groupId, PriceItem request, HttpContext context, DataStore store, AdminSessionStore sessions) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120 || (request.Subtitle?.Length ?? 0) > 500 || (request.PriceText?.Length ?? 0) > 500 || (request.Benefits?.Length ?? 0) > 4000)
                throw new ApiException(400, "Nội dung bảng giá chưa hợp lệ.", "invalid_price_item");
            var actor = sessions.RequireUser(context);
            var item = store.Write(data => {
                var parent = data.PriceCatalog.FirstOrDefault(g => g.Id == groupId)
                    ?? throw new ApiException(404, "Không tìm thấy nhóm bảng giá.", "price_group_not_found");
                var created = new PriceItem { Id = NewShortId(), Name = request.Name.Trim(), Subtitle = (request.Subtitle ?? "").Trim(), PriceText = (request.PriceText ?? "").Trim(), Benefits = (request.Benefits ?? "").Trim(), Visible = request.Visible };
                parent.Items.Add(created);
                StudioAudit.Add(data, actor, "packages", "create", $"đã thêm gói bảng giá “{created.Name}”", created.Id, []);
                return created;
            });
            return Results.Created($"/api/admin/price-catalog/{groupId}/{item.Id}", item);
        }).AddEndpointFilter(packages);
        group.MapPut("/price-catalog/{groupId}/{itemId}", (string groupId, string itemId, PriceItem request, DataStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120 || (request.Subtitle?.Length ?? 0) > 500 || (request.PriceText?.Length ?? 0) > 500 || (request.Benefits?.Length ?? 0) > 4000)
                throw new ApiException(400, "Nội dung bảng giá chưa hợp lệ.", "invalid_price_item");
            return Results.Ok(store.Write(data => {
                var item = data.PriceCatalog.FirstOrDefault(g => g.Id == groupId)?.Items.FirstOrDefault(i => i.Id == itemId)
                    ?? throw new ApiException(404, "Không tìm thấy gói trong bảng giá.", "price_item_not_found");
                item.Name = request.Name.Trim(); item.Subtitle = (request.Subtitle ?? "").Trim(); item.PriceText = (request.PriceText ?? "").Trim(); item.Benefits = (request.Benefits ?? "").Trim(); item.Visible = request.Visible;
                return item;
            }));
        }).AddEndpointFilter(packages);
        group.MapGet("/packages", GetPackages).AddEndpointFilter(packagesRead);
        group.MapPost("/packages", CreatePackage).AddEndpointFilter(packages);
        group.MapPut("/packages/{id}", UpdatePackage).AddEndpointFilter(packages);
        group.MapDelete("/packages/{id}", DeletePackage).AddEndpointFilter(packages);

        group.MapGet("/staff", GetStaff).AddEndpointFilter(staffRead);
        group.MapPost("/staff", CreateStaff).AddEndpointFilter(staff);
        group.MapPut("/staff/{id}", UpdateStaff).AddEndpointFilter(staff);
        group.MapDelete("/staff/{id}", DeleteStaff).AddEndpointFilter(staff);

        group.MapGet("/bookings", GetBookings).AddEndpointFilter(bookingsRead);
        group.MapGet("/bookings/{id}", GetBooking).AddEndpointFilter(bookingsRead);
        group.MapPost("/bookings", CreateBooking).AddEndpointFilter(bookings);
        group.MapPut("/bookings/{id}", UpdateBooking).AddEndpointFilter(bookings);
        group.MapPut("/bookings/{id}/status", UpdateBookingStatus).AddEndpointFilter(bookings);
        group.MapDelete("/bookings/{id}", DeleteBooking).AddEndpointFilter(bookings);
    }

    // ------------------------------------------------------------- customers

    private static IResult GetCustomers(DataStore store)
    {
        var payload = store.Read(data => data.Customers
            .OrderBy(customer => customer.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(customer => customer.Id, StringComparer.Ordinal)
            .Select(customer => ApiMappings.ToCustomer(customer, data.Albums, data.Bookings))
            .ToList());
        return Results.Ok(payload);
    }

    private static IResult GetCustomer(string id, DataStore store)
    {
        var payload = store.Read(data =>
        {
            var customer = FindCustomer(data, id);
            return ApiMappings.ToCustomerDetail(customer, data);
        });
        return Results.Ok(payload);
    }

    private static IResult CreateCustomer(
        CreateCustomerRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var name = RequireNameOrThrow(request.Name, "Tên khách hàng", "invalid_customer_name");
        var type = NormalizeCustomerTypeOrThrow(request.Type);
        var email = NormalizeEmailOrThrow(request.Email);
        var now = DateTimeOffset.UtcNow;

        var dto = store.Write(data =>
        {
            var customer = new Customer
            {
                Id = NewShortId(),
                Name = name,
                Type = type,
                Phone = AlbumText.TrimTo(request.Phone, 40),
                Email = email,
                Address = AlbumText.TrimTo(request.Address, 240),
                Source = AlbumText.TrimTo(request.Source, 80),
                Notes = AlbumText.TrimTo(request.Notes, 2000),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            data.Customers.Add(customer);
            StudioAudit.Add(
                data,
                actor,
                "customers",
                "create",
                $"đã thêm khách hàng “{customer.Name}”",
                customer.Id,
                [
                    string.IsNullOrWhiteSpace(customer.Phone) ? "" : $"Điện thoại: {customer.Phone}",
                    string.IsNullOrWhiteSpace(customer.Email) ? "" : $"Email: {customer.Email}"
                ]);
            return ApiMappings.ToCustomer(customer, data.Albums, data.Bookings);
        });

        return Results.Created($"/api/admin/customers/{dto.Id}", dto);
    }

    private static IResult UpdateCustomer(
        string id,
        UpdateCustomerRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var email = request.Email is null ? null : NormalizeEmailOrThrow(request.Email);
        var type = request.Type is null ? null : NormalizeCustomerTypeOrThrow(request.Type);
        if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ApiException(400, "Tên khách hàng không được để trống.", "invalid_customer_name");
        }

        var dto = store.Write(data =>
        {
            var customer = FindCustomer(data, id);
            var changes = new List<string>();

            if (request.Name is not null)
            {
                var next = RequireNameOrThrow(request.Name, "Tên khách hàng", "invalid_customer_name");
                if (!string.Equals(next, customer.Name, StringComparison.Ordinal))
                {
                    changes.Add($"Tên: {customer.Name} → {next}");
                }

                customer.Name = next;
            }

            if (type is not null)
            {
                customer.Type = type;
            }

            if (request.Phone is not null)
            {
                customer.Phone = AlbumText.TrimTo(request.Phone, 40);
            }

            if (email is not null)
            {
                customer.Email = email;
            }

            if (request.Address is not null)
            {
                customer.Address = AlbumText.TrimTo(request.Address, 240);
            }

            if (request.Source is not null)
            {
                customer.Source = AlbumText.TrimTo(request.Source, 80);
            }

            if (request.Notes is not null)
            {
                customer.Notes = AlbumText.TrimTo(request.Notes, 2000);
            }

            if (request.IsActive is not null)
            {
                customer.IsActive = request.IsActive.Value;
                changes.Add(request.IsActive.Value ? "Kích hoạt khách hàng" : "Lưu trữ khách hàng");
            }

            customer.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "customers",
                "update",
                $"đã cập nhật khách hàng “{customer.Name}”",
                customer.Id,
                changes);
            return ApiMappings.ToCustomer(customer, data.Albums, data.Bookings);
        });

        return Results.Ok(dto);
    }

    private static IResult DeleteCustomer(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        store.Write(data =>
        {
            var customer = FindCustomer(data, id);
            var bookingCount = data.Bookings.Count(booking =>
                string.Equals(booking.CustomerId, customer.Id, StringComparison.Ordinal));
            var albumCount = data.Albums.Count(album =>
                string.Equals(album.CustomerId, customer.Id, StringComparison.Ordinal));
            if (bookingCount > 0 || albumCount > 0)
            {
                throw new ApiException(
                    409,
                    "Khách hàng đang có lịch chụp hoặc album. Hãy lưu trữ khách hàng thay vì xoá.",
                    "customer_in_use");
            }

            data.Customers.Remove(customer);
            StudioAudit.Add(
                data,
                actor,
                "customers",
                "delete",
                $"đã xoá khách hàng “{customer.Name}”",
                customer.Id);
        });

        return Results.NoContent();
    }

    // -------------------------------------------------------------- packages

    private static IResult GetPackages(DataStore store)
    {
        var payload = store.Read(data => data.Packages
            .OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Id, StringComparer.Ordinal)
            .Select(package => ApiMappings.ToPackage(package, data.Bookings))
            .ToList());
        return Results.Ok(payload);
    }

    private static IResult CreatePackage(
        CreatePackageRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var name = RequireNameOrThrow(request.Name, "Tên gói dịch vụ", "invalid_package_name");
        var price = Math.Max(0, request.Price);
        var hours = NormalizeHoursOrThrow(request.Hours);
        var now = DateTimeOffset.UtcNow;

        var dto = store.Write(data =>
        {
            var package = new ServicePackage
            {
                Id = NewShortId(),
                Name = name,
                Category = AlbumText.TrimTo(request.Category, 60),
                Price = price,
                Hours = hours,
                Includes = AlbumText.TrimTo(request.Includes, 4000),
                Notes = AlbumText.TrimTo(request.Notes, 2000),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            data.Packages.Add(package);
            StudioAudit.Add(
                data,
                actor,
                "packages",
                "create",
                $"đã thêm gói dịch vụ “{package.Name}”",
                package.Id,
                [$"{price.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đ"]);
            return ApiMappings.ToPackage(package, data.Bookings);
        });

        return Results.Created($"/api/admin/packages/{dto.Id}", dto);
    }

    private static IResult UpdatePackage(
        string id,
        UpdatePackageRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ApiException(400, "Tên gói dịch vụ không được để trống.", "invalid_package_name");
        }

        var dto = store.Write(data =>
        {
            var package = FindPackage(data, id);
            var changes = new List<string>();

            if (request.Name is not null)
            {
                package.Name = RequireNameOrThrow(request.Name, "Tên gói dịch vụ", "invalid_package_name");
            }

            if (request.Category is not null)
            {
                package.Category = AlbumText.TrimTo(request.Category, 60);
            }

            if (request.Price is not null)
            {
                package.Price = Math.Max(0, request.Price.Value);
                changes.Add($"Giá: {package.Price.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đ");
            }

            if (request.Hours is not null)
            {
                package.Hours = NormalizeHoursOrThrow(request.Hours.Value);
                changes.Add($"Thời lượng: {package.Hours:0.##} giờ");
            }

            if (request.Includes is not null)
            {
                package.Includes = AlbumText.TrimTo(request.Includes, 4000);
            }

            if (request.Notes is not null)
            {
                package.Notes = AlbumText.TrimTo(request.Notes, 2000);
            }

            if (request.IsActive is not null)
            {
                package.IsActive = request.IsActive.Value;
                changes.Add(request.IsActive.Value ? "Đang bán" : "Ngừng bán");
            }

            package.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "packages",
                "update",
                $"đã cập nhật gói dịch vụ “{package.Name}”",
                package.Id,
                changes);
            return ApiMappings.ToPackage(package, data.Bookings);
        });

        return Results.Ok(dto);
    }

    private static IResult DeletePackage(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        store.Write(data =>
        {
            var package = FindPackage(data, id);
            if (data.Bookings.Any(booking =>
                    string.Equals(booking.PackageId, package.Id, StringComparison.Ordinal)))
            {
                throw new ApiException(
                    409,
                    "Gói dịch vụ đang được dùng trong lịch chụp. Hãy ngừng bán thay vì xoá.",
                    "package_in_use");
            }

            data.Packages.Remove(package);
            StudioAudit.Add(
                data,
                actor,
                "packages",
                "delete",
                $"đã xoá gói dịch vụ “{package.Name}”",
                package.Id);
        });

        return Results.NoContent();
    }

    // ----------------------------------------------------------------- staff

    private static IResult GetStaff(DataStore store)
    {
        var payload = store.Read(data => data.Staff
            .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.Id, StringComparer.Ordinal)
            .Select(member => ApiMappings.ToStaff(member, data.Users, data.Bookings))
            .ToList());
        return Results.Ok(payload);
    }

    private static IResult CreateStaff(
        CreateStaffRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var name = RequireNameOrThrow(request.Name, "Tên nhân sự", "invalid_staff_name");
        var email = NormalizeEmailOrThrow(request.Email);
        var now = DateTimeOffset.UtcNow;

        var dto = store.Write(data =>
        {
            var member = new StaffMember
            {
                Id = NewShortId(),
                Name = name,
                Role = NormalizeStaffRole(request.Role),
                Phone = AlbumText.TrimTo(request.Phone, 40),
                Email = email,
                IsActive = true,
                BaseSalary = Math.Max(0, request.BaseSalary),
                CommissionRate = Math.Clamp(request.CommissionRate, 0, 100),
                PayMode = PayModes.Normalize(request.PayMode),
                HourlyRate = Math.Max(0, request.HourlyRate),
                Shift = StaffShifts.Normalize(request.Shift),
                Notes = AlbumText.TrimTo(request.Notes, 2000),
                CreatedAt = now,
                UpdatedAt = now
            };
            data.Staff.Add(member);
            LinkStaffUser(data, member, request.UserId);
            StudioAudit.Add(
                data,
                actor,
                "staff",
                "create",
                $"đã thêm nhân sự “{member.Name}”",
                member.Id,
                [string.IsNullOrWhiteSpace(member.Role) ? "" : $"Vai trò: {member.Role}"]);
            return ApiMappings.ToStaff(member, data.Users, data.Bookings);
        });

        return Results.Created($"/api/admin/staff/{dto.Id}", dto);
    }

    private static IResult UpdateStaff(
        string id,
        UpdateStaffRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ApiException(400, "Tên nhân sự không được để trống.", "invalid_staff_name");
        }

        var email = request.Email is null ? null : NormalizeEmailOrThrow(request.Email);

        var dto = store.Write(data =>
        {
            var member = FindStaff(data, id);
            var changes = new List<string>();

            if (request.Name is not null)
            {
                member.Name = RequireNameOrThrow(request.Name, "Tên nhân sự", "invalid_staff_name");
            }

            if (request.Role is not null)
            {
                member.Role = NormalizeStaffRole(request.Role);
            }

            if (request.Phone is not null)
            {
                member.Phone = AlbumText.TrimTo(request.Phone, 40);
            }

            if (email is not null)
            {
                member.Email = email;
            }

            if (request.BaseSalary is not null)
            {
                member.BaseSalary = Math.Max(0, request.BaseSalary.Value);
            }

            if (request.CommissionRate is not null)
            {
                member.CommissionRate = Math.Clamp(request.CommissionRate.Value, 0, 100);
            }

            if (request.PayMode is not null)
            {
                member.PayMode = PayModes.Normalize(request.PayMode);
            }

            if (request.HourlyRate is not null)
            {
                member.HourlyRate = Math.Max(0, request.HourlyRate.Value);
            }

            if (request.Shift is not null)
            {
                member.Shift = StaffShifts.Normalize(request.Shift);
            }

            if (request.Notes is not null)
            {
                member.Notes = AlbumText.TrimTo(request.Notes, 2000);
            }

            if (request.IsActive is not null)
            {
                member.IsActive = request.IsActive.Value;
                changes.Add(request.IsActive.Value ? "Đang làm việc" : "Đã nghỉ");
            }

            if (request.UserId is not null)
            {
                LinkStaffUser(data, member, request.UserId);
                changes.Add(string.IsNullOrWhiteSpace(member.UserId)
                    ? "Bỏ liên kết tài khoản"
                    : "Liên kết tài khoản đăng nhập");
            }

            member.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "staff",
                "update",
                $"đã cập nhật nhân sự “{member.Name}”",
                member.Id,
                changes);
            return ApiMappings.ToStaff(member, data.Users, data.Bookings);
        });

        return Results.Ok(dto);
    }

    private static IResult DeleteStaff(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        store.Write(data =>
        {
            var member = FindStaff(data, id);
            if (data.Bookings.Any(booking =>
                    string.Equals(booking.PhotographerId, member.Id, StringComparison.Ordinal)))
            {
                throw new ApiException(
                    409,
                    "Nhân sự đang phụ trách lịch chụp. Hãy đánh dấu đã nghỉ thay vì xoá.",
                    "staff_in_use");
            }

            if (!string.IsNullOrWhiteSpace(member.UserId))
            {
                throw new ApiException(
                    409,
                    "Nhân sự đang liên kết với tài khoản đăng nhập. Hãy bỏ liên kết trước.",
                    "staff_linked");
            }

            data.Staff.Remove(member);
            StudioAudit.Add(
                data,
                actor,
                "staff",
                "delete",
                $"đã xoá nhân sự “{member.Name}”",
                member.Id);
        });

        return Results.NoContent();
    }

    // -------------------------------------------------------------- bookings

    private static IResult GetBookings(DataStore store)
    {
        var payload = store.Read(data => data.Bookings
            .OrderByDescending(booking => booking.Date, StringComparer.Ordinal)
            .ThenByDescending(booking => booking.StartTime, StringComparer.Ordinal)
            .Select(booking => ApiMappings.ToBooking(booking, data))
            .ToList());
        return Results.Ok(payload);
    }

    private static IResult GetBooking(string id, DataStore store)
    {
        var payload = store.Read(data => ApiMappings.ToBooking(FindBooking(data, id), data));
        return Results.Ok(payload);
    }

    private static IResult CreateBooking(
        CreateBookingRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var date = RequireDateOrThrow(request.Date);
        var startTime = NormalizeClockOrThrow(request.StartTime);
        var status = NormalizeBookingStatusOrThrow(request.Status);
        var hours = request.Hours is null ? (decimal?)null : NormalizeHoursOrThrow(request.Hours.Value);
        var now = DateTimeOffset.UtcNow;

        var dto = store.Write(data =>
        {
            var customer = ResolveCustomer(data, request.CustomerId);
            var package = ResolvePackage(data, request.PackageId);
            var photographer = ResolveStaff(data, request.PhotographerId);
            var album = ResolveAlbum(data, request.AlbumId);
            var booking = new Booking
            {
                Id = NewShortId(),
                Code = NextBookingCode(data.Bookings),
                CustomerId = customer?.Id ?? "",
                PackageId = package?.Id ?? "",
                Date = date,
                StartTime = startTime,
                Hours = hours ?? package?.Hours ?? 0,
                Location = AlbumText.TrimTo(request.Location, 160),
                PhotographerId = photographer?.Id ?? "",
                Status = status,
                Notes = AlbumText.TrimTo(request.Notes, 2000),
                AlbumId = album?.Id ?? "",
                Price = Math.Max(0, request.Price ?? package?.Price ?? 0),
                CreatedAt = now,
                UpdatedAt = now
            };
            booking.StatusHistory.Add(new BookingStatusEntry
            {
                Status = status,
                ActorName = ActorName(actor),
                ChangedAt = now
            });

            if (album is not null)
            {
                LinkBookingAlbum(album, booking, customer);
            }

            data.Bookings.Add(booking);
            StudioAudit.Add(
                data,
                actor,
                "bookings",
                "create",
                $"đã tạo lịch chụp {booking.Code} cho “{customer?.Name ?? "khách lẻ"}”",
                booking.Id,
                [
                    $"{date} lúc {startTime}",
                    package is null ? "" : $"Gói: {package.Name}",
                    photographer is null ? "" : $"Phụ trách: {photographer.Name}"
                ]);
            StudioNotifications.Push(
                data,
                NotificationTypes.Booking,
                $"Lịch chụp mới: {booking.Code}",
                $"{customer?.Name ?? "Khách lẻ"} — {date} lúc {startTime}" +
                (package is null ? "" : $" — {package.Name}"),
                "/admin/bookings");
            return ApiMappings.ToBooking(booking, data);
        });

        return Results.Created($"/api/admin/bookings/{dto.Id}", dto);
    }

    private static IResult UpdateBooking(
        string id,
        UpdateBookingRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var date = request.Date is null ? null : RequireDateOrThrow(request.Date);
        var startTime = request.StartTime is null ? null : NormalizeClockOrThrow(request.StartTime);
        var status = request.Status is null ? null : NormalizeBookingStatusOrThrow(request.Status);
        var hours = request.Hours is null ? (decimal?)null : NormalizeHoursOrThrow(request.Hours.Value);

        var dto = store.Write(data =>
        {
            var booking = FindBooking(data, id);
            var changes = new List<string>();

            if (request.CustomerId is not null)
            {
                var customer = ResolveCustomer(data, request.CustomerId);
                if (!string.Equals(customer?.Id ?? "", booking.CustomerId, StringComparison.Ordinal))
                {
                    changes.Add($"Khách hàng: {customer?.Name ?? "Khách lẻ"}");
                }

                booking.CustomerId = customer?.Id ?? "";
            }

            if (request.PackageId is not null)
            {
                var package = ResolvePackage(data, request.PackageId);
                booking.PackageId = package?.Id ?? "";
                if (package is not null && request.Price is null && booking.Price <= 0)
                {
                    booking.Price = package.Price;
                }

                if (package is not null && request.Hours is null && booking.Hours <= 0)
                {
                    booking.Hours = package.Hours;
                }
            }

            if (date is not null)
            {
                booking.Date = date;
                changes.Add($"Ngày: {date}");
            }

            if (startTime is not null)
            {
                booking.StartTime = startTime;
            }

            if (hours is not null)
            {
                booking.Hours = hours.Value;
            }

            if (request.Location is not null)
            {
                booking.Location = AlbumText.TrimTo(request.Location, 160);
            }

            if (request.PhotographerId is not null)
            {
                var photographer = ResolveStaff(data, request.PhotographerId);
                if (!string.Equals(photographer?.Id ?? "", booking.PhotographerId, StringComparison.Ordinal))
                {
                    changes.Add($"Phụ trách: {photographer?.Name ?? "Chưa phân công"}");
                }

                booking.PhotographerId = photographer?.Id ?? "";
            }

            if (status is not null)
            {
                ApplyStatusChange(booking, status, actor, changes);
            }

            if (request.Notes is not null)
            {
                booking.Notes = AlbumText.TrimTo(request.Notes, 2000);
            }

            if (request.AlbumId is not null)
            {
                var album = ResolveAlbum(data, request.AlbumId);
                booking.AlbumId = album?.Id ?? "";
                if (album is not null)
                {
                    var customer = data.Customers.FirstOrDefault(candidate =>
                        string.Equals(candidate.Id, booking.CustomerId, StringComparison.Ordinal));
                    LinkBookingAlbum(album, booking, customer);
                }
            }

            if (request.Price is not null)
            {
                booking.Price = Math.Max(0, request.Price.Value);
                changes.Add($"Giá: {booking.Price.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đ");
            }

            booking.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "bookings",
                "update",
                $"đã cập nhật lịch chụp {booking.Code}",
                booking.Id,
                changes);
            return ApiMappings.ToBooking(booking, data);
        });

        return Results.Ok(dto);
    }

    private static IResult UpdateBookingStatus(
        string id,
        UpdateBookingStatusRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var status = NormalizeBookingStatusOrThrow(request.Status);
        var dto = store.Write(data =>
        {
            var booking = FindBooking(data, id);
            var changes = new List<string>();
            ApplyStatusChange(booking, status, actor, changes);
            booking.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "bookings",
                "status",
                $"đã đổi trạng thái lịch chụp {booking.Code}",
                booking.Id,
                changes);
            if (changes.Count > 0)
            {
                StudioNotifications.Push(
                    data,
                    NotificationTypes.Booking,
                    $"Lịch chụp {booking.Code} đã cập nhật",
                    changes[0],
                    "/admin/bookings");
            }
            return ApiMappings.ToBooking(booking, data);
        });

        return Results.Ok(dto);
    }

    private static IResult DeleteBooking(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        store.Write(data =>
        {
            var booking = FindBooking(data, id);
            data.Bookings.Remove(booking);
            foreach (var card in data.Board.Lists.SelectMany(list => list.Cards))
            {
                if (string.Equals(card.BookingId, booking.Id, StringComparison.Ordinal))
                {
                    card.BookingId = "";
                }
            }

            StudioAudit.Add(
                data,
                actor,
                "bookings",
                "delete",
                $"đã xoá lịch chụp {booking.Code}",
                booking.Id);
        });

        return Results.NoContent();
    }

    // --------------------------------------------------------------- summary

    private static IResult GetSummary(DataStore store)
    {
        var payload = store.Read(data => ApiMappings.ToCrmSummary(data, Today()));
        return Results.Ok(payload);
    }

    // ----------------------------------------------------------------- audit

    private static IResult GetAudit(DataStore store, int? limit, string? module, string? actor)
    {
        var take = Math.Clamp(limit ?? 200, 1, 500);
        var payload = store.Read(data => data.AuditLogs
            .Where(entry => string.IsNullOrWhiteSpace(module) ||
                            string.Equals(entry.Module, module, StringComparison.OrdinalIgnoreCase))
            .Where(entry => string.IsNullOrWhiteSpace(actor) ||
                            string.Equals(entry.ActorId, actor, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.CreatedAt)
            .Take(take)
            .Select(ApiMappings.ToAuditLog)
            .ToList());
        return Results.Ok(payload);
    }

    // ---------------------------------------------------------------- import

    private static IResult ImportBackup(
        ImportCrmRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        if (string.IsNullOrWhiteSpace(request.Json))
        {
            throw new ApiException(400, "Chưa có nội dung JSON để nhập.", "invalid_json");
        }

        var bundle = ParseBackup(request.Json);
        ImportCrmResultDto result;
        if (request.DryRun)
        {
            result = store.ReadClone(data =>
                ApplyImport(data, bundle, request.ReplaceExisting, null, true));
        }
        else
        {
            result = store.Write(data =>
                ApplyImport(data, bundle, request.ReplaceExisting, actor, false));
        }

        return Results.Ok(result);
    }

    private static ImportBundle ParseBackup(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new ApiException(400, "Tệp JSON không hợp lệ.", "invalid_json");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("customers", out var customersElement) ||
                customersElement.ValueKind != JsonValueKind.Array)
            {
                throw new ApiException(
                    400,
                    "Tệp JSON không phải bản sao lưu CRM (thiếu danh sách khách hàng).",
                    "invalid_backup");
            }

            CrmBackup backup;
            try
            {
                backup = root.Deserialize<CrmBackup>(ImportJsonOptions) ?? new CrmBackup();
            }
            catch (JsonException)
            {
                throw new ApiException(400, "Cấu trúc JSON không đúng định dạng CRM.", "invalid_backup");
            }

            var bundle = new ImportBundle();
            foreach (var (key, label) in UnsupportedCollections)
            {
                if (!root.TryGetProperty(key, out var element))
                {
                    continue;
                }

                var count = UnsupportedCount(element);
                if (count <= 0)
                {
                    continue;
                }

                bundle.Skipped += count;
                Warn(bundle, $"Bỏ qua {count} {label} — chưa hỗ trợ trong bản này.");
            }

            if (root.TryGetProperty("settings", out var settingsElement) &&
                settingsElement.ValueKind == JsonValueKind.Object &&
                settingsElement.EnumerateObject().Any())
            {
                Warn(bundle, "Bỏ qua cài đặt CRM để giữ thông tin studio hiện tại.");
            }

            var now = DateTimeOffset.UtcNow;
            var seenCustomers = new HashSet<string>(StringComparer.Ordinal);
            var seenPackages = new HashSet<string>(StringComparer.Ordinal);
            var seenStaff = new HashSet<string>(StringComparer.Ordinal);
            var seenBookings = new HashSet<string>(StringComparer.Ordinal);
            var seenOrders = new HashSet<string>(StringComparer.Ordinal);
            var seenPayments = new HashSet<string>(StringComparer.Ordinal);
            var seenContracts = new HashSet<string>(StringComparer.Ordinal);
            var seenExpenses = new HashSet<string>(StringComparer.Ordinal);
            var seenPayrolls = new HashSet<string>(StringComparer.Ordinal);
            var seenAttendance = new HashSet<string>(StringComparer.Ordinal);
            var seenOutfits = new HashSet<string>(StringComparer.Ordinal);
            var seenConcepts = new HashSet<string>(StringComparer.Ordinal);
            var seenLoans = new HashSet<string>(StringComparer.Ordinal);
            var seenNotifications = new HashSet<string>(StringComparer.Ordinal);
            var attendanceKeys = new HashSet<string>(StringComparer.Ordinal);
            var payrollKeys = new HashSet<string>(StringComparer.Ordinal);
            var customerMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var bookingMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var staffMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var outfitMap = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var source in backup.Customers ?? [])
            {
                var name = AlbumText.TrimTo(source.Name, 120);
                if (string.IsNullOrWhiteSpace(name))
                {
                    bundle.Skipped++;
                    Warn(bundle, "Bỏ qua một khách hàng không có tên.");
                    continue;
                }

                var sourceId = CleanImportId(source.Id);
                var id = UniqueImportId(sourceId, seenCustomers, "khách hàng", bundle);
                if (sourceId.Length > 0 && !customerMap.ContainsKey(sourceId))
                {
                    customerMap[sourceId] = id;
                }

                var createdAt = ParseTimestamp(source.CreatedAt, now);
                bundle.Customers.Add(new Customer
                {
                    Id = id,
                    Name = name,
                    Type = string.Equals(source.Type, "company", StringComparison.OrdinalIgnoreCase)
                        ? "company"
                        : "individual",
                    Phone = AlbumText.TrimTo(source.Phone, 40),
                    Email = AlbumText.TrimTo(source.Email, 160),
                    Address = AlbumText.TrimTo(source.Address, 240),
                    Source = AlbumText.TrimTo(source.Source, 80),
                    Notes = AlbumText.TrimTo(source.Notes, 2000),
                    IsActive = source.Active ?? true,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                });
            }

            foreach (var source in backup.Packages ?? [])
            {
                var name = AlbumText.TrimTo(source.Name, 120);
                if (string.IsNullOrWhiteSpace(name))
                {
                    bundle.Skipped++;
                    Warn(bundle, "Bỏ qua một gói dịch vụ không có tên.");
                    continue;
                }

                var id = UniqueImportId(CleanImportId(source.Id), seenPackages, "gói dịch vụ", bundle);
                var createdAt = ParseTimestamp(source.CreatedAt, now);
                bundle.Packages.Add(new ServicePackage
                {
                    Id = id,
                    Name = name,
                    Category = AlbumText.TrimTo(source.Category, 60),
                    Price = Math.Max(0, source.Price ?? 0),
                    Hours = Math.Clamp(source.Hours ?? 0, 0, 1000),
                    Includes = NormalizeIncludes(source.Includes),
                    Notes = AlbumText.TrimTo(source.Notes, 2000),
                    IsActive = source.Active ?? true,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                });
            }

            foreach (var source in backup.Staff ?? [])
            {
                var name = AlbumText.TrimTo(source.Name, 120);
                if (string.IsNullOrWhiteSpace(name))
                {
                    bundle.Skipped++;
                    Warn(bundle, "Bỏ qua một nhân sự không có tên.");
                    continue;
                }

                var id = UniqueImportId(CleanImportId(source.Id), seenStaff, "nhân sự", bundle);
                var sourceStaffId = CleanImportId(source.Id);
                if (sourceStaffId.Length > 0 && !staffMap.ContainsKey(sourceStaffId))
                {
                    staffMap[sourceStaffId] = id;
                }
                var createdAt = ParseTimestamp(source.CreatedAt, now);
                bundle.Staff.Add(new StaffMember
                {
                    Id = id,
                    Name = name,
                    Role = NormalizeStaffRole(source.Role),
                    Phone = AlbumText.TrimTo(source.Phone, 40),
                    Email = AlbumText.TrimTo(source.Email, 160),
                    IsActive = source.Active ?? true,
                    BaseSalary = Math.Max(0, source.Salary ?? 0),
                    CommissionRate = Math.Clamp(source.CommissionPct ?? 0, 0, 100),
                    PayMode = PayModes.Normalize(source.PayMode),
                    HourlyRate = Math.Max(0, source.HourlyRate ?? 0),
                    Shift = StaffShifts.Normalize(source.Shift),
                    Notes = AlbumText.TrimTo(source.Notes, 2000),
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                });
            }

            foreach (var source in backup.Bookings ?? [])
            {
                var date = AlbumText.NormalizeEventDate(source.Date);
                if (string.IsNullOrWhiteSpace(date))
                {
                    bundle.Skipped++;
                    Warn(bundle, $"Bỏ qua lịch chụp “{AlbumText.TrimTo(source.Id, 40)}” vì ngày không hợp lệ.");
                    continue;
                }

                var id = UniqueImportId(CleanImportId(source.Id), seenBookings, "lịch chụp", bundle);
                var sourceBookingId = CleanImportId(source.Id);
                if (sourceBookingId.Length > 0 && !bookingMap.ContainsKey(sourceBookingId))
                {
                    bookingMap[sourceBookingId] = id;
                }
                var status = BookingStatuses.IsKnown(source.Status)
                    ? source.Status!
                    : BookingStatuses.Pending;
                var createdAt = ParseTimestamp(source.CreatedAt, now);
                var galleryPhotoCount = source.Gallery?.Photos?.Count ?? 0;
                var dropped = new List<string>();
                if (galleryPhotoCount > 0)
                {
                    dropped.Add($"{galleryPhotoCount} ảnh Drive");
                }

                if (!string.IsNullOrWhiteSpace(source.PortalToken))
                {
                    dropped.Add("cổng khách hàng");
                }

                if (dropped.Count > 0)
                {
                    Warn(
                        bundle,
                        $"Bỏ qua {string.Join(" và ", dropped)} của lịch chụp “{AlbumText.TrimTo(source.Id, 40)}” — hãy tạo album Drive riêng nếu cần.");
                }

                var customerId = source.CustomerId ?? "";
                if (customerId.Length > 0 && customerMap.TryGetValue(customerId, out var mappedCustomer))
                {
                    customerId = mappedCustomer;
                }

                var booking = new Booking
                {
                    Id = id,
                    CustomerId = AlbumText.TrimTo(customerId, 40),
                    PackageId = AlbumText.TrimTo(source.PackageId, 40),
                    Date = date,
                    StartTime = NormalizeClockOrDefault(source.StartTime),
                    Hours = Math.Clamp(source.Hours ?? 0, 0, 1000),
                    Location = AlbumText.TrimTo(source.Location, 160),
                    PhotographerId = AlbumText.TrimTo(source.PhotographerId, 40),
                    Status = status,
                    Notes = AlbumText.TrimTo(source.Notes, 2000),
                    Price = Math.Max(0, source.Price ?? 0),
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                };
                booking.StatusHistory.Add(new BookingStatusEntry
                {
                    Status = status,
                    ActorName = "CRM",
                    ChangedAt = createdAt
                });
                bundle.Bookings.Add(booking);
            }

            foreach (var source in backup.Orders ?? [])
            {
                var sourceId = CleanImportId(source.Id);
                var items = new List<OrderItem>();
                foreach (var item in source.Items ?? [])
                {
                    var name = AlbumText.TrimTo(item?.Name, 160);
                    var quantity = Math.Clamp(item?.Qty ?? 1, 0, 10000);
                    if (name.Length == 0 || quantity <= 0)
                    {
                        continue;
                    }

                    items.Add(new OrderItem
                    {
                        Name = name,
                        Quantity = quantity,
                        UnitPrice = Math.Max(0, Math.Round(item?.Price ?? 0, 0))
                    });
                }

                if (items.Count == 0)
                {
                    bundle.Skipped++;
                    Warn(bundle, $"Bỏ qua đơn hàng “{AlbumText.TrimTo(source.Code ?? sourceId, 40)}” vì không có hạng mục hợp lệ.");
                    continue;
                }

                var sourceCustomerId = CleanImportId(source.CustomerId);
                if (!customerMap.TryGetValue(sourceCustomerId, out var mappedCustomer))
                {
                    bundle.Skipped++;
                    Warn(bundle, $"Bỏ qua đơn hàng “{AlbumText.TrimTo(source.Code ?? sourceId, 40)}” vì khách hàng không tồn tại trong bản sao lưu.");
                    continue;
                }

                var id = UniqueImportId(sourceId, seenOrders, "đơn hàng", bundle);
                var createdAt = ParseTimestamp(source.CreatedAt, now);
                var bookingId = "";
                var sourceBookingId = CleanImportId(source.BookingId);
                if (sourceBookingId.Length > 0 && bookingMap.TryGetValue(sourceBookingId, out var mappedBooking))
                {
                    bookingId = mappedBooking;
                }
                else if (sourceBookingId.Length > 0)
                {
                    Warn(bundle, $"Đơn hàng “{AlbumText.TrimTo(source.Code ?? sourceId, 40)}”: lịch chụp không còn tồn tại, đã bỏ liên kết.");
                }

                bundle.Orders.Add(new Order
                {
                    Id = id,
                    Code = AlbumText.TrimTo(source.Code, 24),
                    CustomerId = mappedCustomer,
                    BookingId = bookingId,
                    Items = items,
                    Discount = Math.Max(0, Math.Round(source.Discount ?? 0, 0)),
                    Status = OrderStatuses.IsKnown(source.Status) ? source.Status! : OrderStatuses.Confirmed,
                    Notes = AlbumText.TrimTo(source.Notes, 2000),
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                });

                var opening = Math.Max(0, Math.Round(source.PaidAmount ?? 0, 0));
                if (opening > 0 && sourceId.Length > 0)
                {
                    bundle.OpeningBalances.Add(new OpeningBalance(
                        sourceId,
                        opening,
                        createdAt.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        mappedCustomer));
                }
            }

            foreach (var source in backup.Payments ?? [])
            {
                var amount = Math.Max(0, Math.Round(source.Amount ?? 0, 0));
                if (amount <= 0)
                {
                    bundle.Skipped++;
                    Warn(bundle, "Bỏ qua một khoản thanh toán không có số tiền hợp lệ.");
                    continue;
                }

                var sourceOrderId = CleanImportId(source.OrderId);
                if (sourceOrderId.Length == 0)
                {
                    bundle.Skipped++;
                    Warn(bundle, $"Bỏ qua thanh toán {FormatImportMoney(amount)} chưa gắn với đơn hàng.");
                    continue;
                }

                var id = UniqueImportId(CleanImportId(source.Id), seenPayments, "thanh toán", bundle);
                var paidAt = AlbumText.NormalizeEventDate(source.Ts);
                if (paidAt.Length == 0)
                {
                    paidAt = now.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                var note = AlbumText.TrimTo(source.Note, 500);
                if (note.Length == 0)
                {
                    note = AlbumText.TrimTo(source.Content, 500);
                }

                var method = PaymentMethods.IsKnown(source.Method)
                    ? source.Method!
                    : PaymentMethods.Transfer;
                bundle.Payments.Add(new Payment
                {
                    Id = id,
                    Code = "",
                    OrderId = sourceOrderId,
                    Amount = amount,
                    Method = method,
                    PaidAt = paidAt,
                    Note = note,
                    ActorName = "CRM",
                    CreatedAt = ParseTimestamp(source.Ts, now)
                });
            }

            foreach (var source in backup.Contracts ?? [])
            {
                var sourceId = CleanImportId(source.Id);
                var sourceCustomerId = CleanImportId(source.CustomerId);
                if (!customerMap.TryGetValue(sourceCustomerId, out var mappedCustomer))
                {
                    bundle.Skipped++;
                    Warn(bundle, $"Bỏ qua hợp đồng “{AlbumText.TrimTo(source.Code ?? sourceId, 40)}” vì khách hàng không tồn tại trong bản sao lưu.");
                    continue;
                }

                var schedule = new List<ContractScheduleItem>();
                foreach (var item in source.Schedule ?? [])
                {
                    var amount = Math.Max(0, Math.Round(item?.Amount ?? 0, 0));
                    var dueDate = AlbumText.NormalizeEventDate(item?.DueDate);
                    var paid = item?.Paid ?? false;
                    var paidAt = AlbumText.NormalizeEventDate(item?.PaidAt);
                    if (paid && paidAt.Length == 0)
                    {
                        paidAt = dueDate;
                    }

                    schedule.Add(new ContractScheduleItem
                    {
                        Label = AlbumText.TrimTo(item?.Label, 80) is { Length: > 0 } label ? label : "Thanh toán",
                        Amount = amount,
                        DueDate = dueDate,
                        Paid = paid,
                        PaidAt = paid ? paidAt : ""
                    });
                }

                var total = Math.Max(0, Math.Round(source.Total ?? schedule.Sum(item => item.Amount), 0));
                if (total <= 0)
                {
                    bundle.Skipped++;
                    Warn(bundle, $"Bỏ qua hợp đồng “{AlbumText.TrimTo(source.Code ?? sourceId, 40)}” vì giá trị không hợp lệ.");
                    continue;
                }

                if (schedule.Count == 0)
                {
                    schedule.Add(new ContractScheduleItem
                    {
                        Label = "Thanh toán toàn bộ (100%)",
                        Amount = total,
                        DueDate = ParseTimestamp(source.CreatedAt, now)
                            .ToOffset(TimeSpan.FromHours(7))
                            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    });
                }
                else
                {
                    var scheduled = schedule.Sum(item => item.Amount);
                    if (scheduled != total)
                    {
                        var last = schedule[^1];
                        last.Amount = Math.Max(0, last.Amount + total - scheduled);
                    }
                }

                var id = UniqueImportId(sourceId, seenContracts, "hợp đồng", bundle);
                var createdAt = ParseTimestamp(source.CreatedAt, now);
                var bookingId = "";
                var sourceBookingId = CleanImportId(source.BookingId);
                if (sourceBookingId.Length > 0 && bookingMap.TryGetValue(sourceBookingId, out var mappedBooking))
                {
                    bookingId = mappedBooking;
                }
                else if (sourceBookingId.Length > 0)
                {
                    Warn(bundle, $"Hợp đồng “{AlbumText.TrimTo(source.Code ?? sourceId, 40)}”: lịch chụp không còn tồn tại, đã bỏ liên kết.");
                }

                bundle.Contracts.Add(new Contract
                {
                    Id = id,
                    Code = AlbumText.TrimTo(source.Code, 24),
                    CustomerId = mappedCustomer,
                    BookingId = bookingId,
                    Service = AlbumText.TrimTo(source.Service, 160),
                    Total = total,
                    Schedule = schedule,
                    Terms = AlbumText.TrimTo(source.Terms, 8000),
                    Status = ContractStatuses.IsKnown(source.Status)
                        ? source.Status!
                        : ContractStatuses.Pending,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                });
            }

            foreach (var source in backup.Expenses ?? [])
            {
                var amount = Math.Max(0, Math.Round(source.Amount ?? 0, 0));
                if (amount <= 0)
                {
                    bundle.Skipped++;
                    Warn(bundle, "Bỏ qua một khoản chi không có số tiền hợp lệ.");
                    continue;
                }

                var date = AlbumText.NormalizeEventDate(source.Date);
                if (date.Length == 0)
                {
                    date = now.ToOffset(TimeSpan.FromHours(7))
                        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                bundle.Expenses.Add(new ExpenseEntry
                {
                    Id = UniqueImportId(CleanImportId(source.Id), seenExpenses, "chi phí", bundle),
                    Date = date,
                    Category = ExpenseCategories.Normalize(source.Category),
                    Amount = amount,
                    Note = AlbumText.TrimTo(source.Note, 500),
                    CreatedByName = "CRM",
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            foreach (var source in backup.Outfits ?? [])
            {
                var name = AlbumText.TrimTo(source.Name, 120);
                if (name.Length == 0)
                {
                    bundle.Skipped++;
                    Warn(bundle, "Bỏ qua một trang phục không có tên.");
                    continue;
                }

                var id = UniqueImportId(CleanImportId(source.Id), seenOutfits, "trang phục", bundle);
                var sourceOutfitId = CleanImportId(source.Id);
                if (sourceOutfitId.Length > 0 && !outfitMap.ContainsKey(sourceOutfitId))
                {
                    outfitMap[sourceOutfitId] = id;
                }

                bundle.Outfits.Add(new Outfit
                {
                    Id = id,
                    Name = name,
                    Category = OutfitCategories.Normalize(source.Category),
                    Size = AlbumText.TrimTo(source.Size, 40),
                    Color = AlbumText.TrimTo(source.Color, 60),
                    Quantity = Math.Clamp(source.Qty ?? 1, 0, 10000),
                    Note = AlbumText.TrimTo(source.Note, 1000),
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            foreach (var source in backup.Concepts ?? [])
            {
                var name = AlbumText.TrimTo(source.Name, 120);
                if (name.Length == 0)
                {
                    bundle.Skipped++;
                    Warn(bundle, "Bỏ qua một concept không có tên.");
                    continue;
                }

                bundle.Concepts.Add(new StudioConcept
                {
                    Id = UniqueImportId(CleanImportId(source.Id), seenConcepts, "concept", bundle),
                    Name = name,
                    Description = AlbumText.TrimTo(source.Desc, 2000),
                    Fee = Math.Max(0, Math.Round(source.Fee ?? 0, 0)),
                    Status = ConceptStatuses.Normalize(source.Status),
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            foreach (var source in backup.Loans ?? [])
            {
                var sourceOutfitId = CleanImportId(source.OutfitId);
                if (!outfitMap.TryGetValue(sourceOutfitId, out var mappedOutfit))
                {
                    bundle.Skipped++;
                    Warn(bundle, $"Bỏ qua phiếu cho thuê “{AlbumText.TrimTo(source.Id, 40)}” vì trang phục không tồn tại.");
                    continue;
                }

                var borrowDate = AlbumText.NormalizeEventDate(source.BorrowDate);
                if (borrowDate.Length == 0)
                {
                    borrowDate = AlbumText.NormalizeEventDate(source.CreatedAt);
                }

                if (borrowDate.Length == 0)
                {
                    borrowDate = now.ToOffset(TimeSpan.FromHours(7))
                        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                var customerId = "";
                var sourceCustomerId = CleanImportId(source.CustomerId);
                if (sourceCustomerId.Length > 0 &&
                    customerMap.TryGetValue(sourceCustomerId, out var mappedCustomer))
                {
                    customerId = mappedCustomer;
                }
                else if (sourceCustomerId.Length > 0)
                {
                    Warn(bundle, $"Phiếu cho thuê “{AlbumText.TrimTo(source.Id, 40)}”: khách hàng không còn tồn tại, đã bỏ liên kết.");
                }

                var staffId = "";
                var sourceStaffId = CleanImportId(source.StaffId);
                if (sourceStaffId.Length > 0 && staffMap.TryGetValue(sourceStaffId, out var mappedStaff))
                {
                    staffId = mappedStaff;
                }

                var bookingId = "";
                var sourceBookingId = CleanImportId(source.BookingId);
                if (sourceBookingId.Length > 0 && bookingMap.TryGetValue(sourceBookingId, out var mappedBooking))
                {
                    bookingId = mappedBooking;
                }
                else if (sourceBookingId.Length > 0)
                {
                    Warn(bundle, $"Phiếu cho thuê “{AlbumText.TrimTo(source.Id, 40)}”: lịch chụp không còn tồn tại, đã bỏ liên kết.");
                }

                bundle.Loans.Add(new OutfitLoan
                {
                    Id = UniqueImportId(CleanImportId(source.Id), seenLoans, "phiếu cho thuê", bundle),
                    OutfitId = mappedOutfit,
                    Quantity = Math.Clamp(source.Qty ?? 1, 1, 10000),
                    BorrowerType = LoanBorrowerTypes.Normalize(source.BorrowerType),
                    CustomerId = customerId,
                    StaffId = staffId,
                    BookingId = bookingId,
                    BorrowDate = borrowDate,
                    DueDate = AlbumText.NormalizeEventDate(source.DueDate),
                    ReturnDate = AlbumText.NormalizeEventDate(source.ReturnDate),
                    Deposit = Math.Max(0, Math.Round(source.Deposit ?? 0, 0)),
                    Notes = AlbumText.TrimTo(source.Notes, 1000),
                    CreatedAt = ParseTimestamp(source.CreatedAt, now),
                    UpdatedAt = now
                });
            }

            foreach (var source in backup.Payrolls ?? [])
            {
                var sourceStaffId = CleanImportId(source.StaffId);
                if (!staffMap.TryGetValue(sourceStaffId, out var mappedStaff))
                {
                    bundle.Skipped++;
                    Warn(bundle, $"Bỏ qua bảng lương tháng {AlbumText.TrimTo(source.Month, 20)} vì nhân sự không tồn tại.");
                    continue;
                }

                var month = NormalizeImportMonth(source.Month);
                if (month.Length == 0)
                {
                    bundle.Skipped++;
                    Warn(bundle, "Bỏ qua một bảng lương có tháng không hợp lệ.");
                    continue;
                }

                if (!payrollKeys.Add(mappedStaff + "|" + month))
                {
                    bundle.Skipped++;
                    Warn(bundle, $"Bỏ qua bảng lương trùng tháng {month} của cùng một nhân sự.");
                    continue;
                }

                var basePay = Math.Max(0, Math.Round(source.Base ?? 0, 0));
                var hours = Math.Max(0, Math.Round(source.Hours ?? 0, 2));
                var commissionBase = Math.Max(0, Math.Round(source.CommissionBase ?? 0, 0));
                var commission = Math.Max(0, Math.Round(source.Commission ?? 0, 0));
                var total = Math.Max(0, Math.Round(source.Total ?? basePay + commission, 0));
                var paidAt = AlbumText.NormalizeEventDate(source.PaidDate);
                var paid = source.Paid ?? false;
                if (paid && paidAt.Length == 0)
                {
                    paidAt = month + "-01";
                }

                bundle.Payrolls.Add(new PayrollEntry
                {
                    Id = UniqueImportId(CleanImportId(source.Id), seenPayrolls, "bảng lương", bundle),
                    Month = month,
                    StaffId = mappedStaff,
                    Base = basePay,
                    Hours = hours,
                    CommissionBase = commissionBase,
                    Commission = commission,
                    Total = total,
                    Paid = paid,
                    PaidAt = paid ? paidAt : "",
                    ComputedAt = now,
                    UpdatedAt = now
                });
            }

            var missingAttendanceStaff = 0;
            foreach (var source in backup.Attendance ?? [])
            {
                var sourceStaffId = CleanImportId(source.StaffId);
                if (!staffMap.TryGetValue(sourceStaffId, out var mappedStaff))
                {
                    bundle.Skipped++;
                    missingAttendanceStaff++;
                    continue;
                }

                var date = AlbumText.NormalizeEventDate(source.Date);
                if (date.Length == 0)
                {
                    bundle.Skipped++;
                    continue;
                }

                if (!attendanceKeys.Add(mappedStaff + "|" + date))
                {
                    bundle.Skipped++;
                    continue;
                }

                bundle.Attendance.Add(new AttendanceRecord
                {
                    Id = UniqueImportId(CleanImportId(source.Id), seenAttendance, "chấm công", bundle),
                    StaffId = mappedStaff,
                    Date = date,
                    In = NormalizeClockOrEmpty(source.In),
                    Out = NormalizeClockOrEmpty(source.Out),
                    Note = AlbumText.TrimTo(source.Note, 500),
                    CreatedAt = ParseTimestamp(source.CreatedAt, now),
                    UpdatedAt = now
                });
            }

            if (missingAttendanceStaff > 0)
            {
                Warn(bundle, $"Bỏ qua {missingAttendanceStaff} bản ghi chấm công vì nhân sự không tồn tại trong studio.");
            }

            foreach (var source in backup.Notifications ?? [])
            {
                var title = AlbumText.TrimTo(source.Title, 160);
                if (title.Length == 0)
                {
                    bundle.Skipped++;
                    continue;
                }

                bundle.Notifications.Add(new NotificationEntry
                {
                    Id = UniqueImportId(CleanImportId(source.Id), seenNotifications, "thông báo", bundle),
                    Type = NotificationTypes.Normalize(source.Type),
                    Title = title,
                    Body = AlbumText.TrimTo(source.Body, 600),
                    Link = "",
                    IsRead = source.Read ?? false,
                    CreatedAt = ParseTimestamp(source.Ts, now)
                });
            }

            return bundle;
        }
    }

    private static ImportCrmResultDto ApplyImport(
        AppData data,
        ImportBundle bundle,
        bool replaceExisting,
        StudioUser? actor,
        bool dryRun)
    {
        var warnings = new List<string>(bundle.Warnings);
        var skipped = bundle.Skipped;
        var customerCount = 0;
        var packageCount = 0;
        var staffCount = 0;
        var bookingCount = 0;
        var orderCount = 0;
        var paymentCount = 0;
        var contractCount = 0;
        var expenseCount = 0;
        var payrollCount = 0;
        var attendanceCount = 0;
        var outfitCount = 0;
        var conceptCount = 0;
        var loanCount = 0;
        var notificationCount = 0;
        var orderMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var skippedOrderIds = new HashSet<string>(StringComparer.Ordinal);
        var importedPaymentsByOrder = new Dictionary<string, decimal>(StringComparer.Ordinal);

        if (replaceExisting)
        {
            data.Customers = [.. bundle.Customers];
            data.Packages = [.. bundle.Packages];
            data.Staff = [.. bundle.Staff];
            data.Bookings = [.. bundle.Bookings];
            data.Orders = [.. bundle.Orders];
            data.Contracts = [.. bundle.Contracts];
            data.Expenses = [.. bundle.Expenses];
            data.Payrolls = [.. bundle.Payrolls];
            data.Attendance = [.. bundle.Attendance];
            data.Outfits = [.. bundle.Outfits];
            data.Concepts = [.. bundle.Concepts];
            data.OutfitLoans = [.. bundle.Loans];
            data.Notifications = [.. bundle.Notifications];
            customerCount = data.Customers.Count;
            packageCount = data.Packages.Count;
            staffCount = data.Staff.Count;
            bookingCount = data.Bookings.Count;
            orderCount = data.Orders.Count;
            contractCount = data.Contracts.Count;
            expenseCount = data.Expenses.Count;
            payrollCount = data.Payrolls.Count;
            attendanceCount = data.Attendance.Count;
            outfitCount = data.Outfits.Count;
            conceptCount = data.Concepts.Count;
            loanCount = data.OutfitLoans.Count;
            notificationCount = data.Notifications.Count;
            foreach (var order in data.Orders)
            {
                orderMap[order.Id] = order.Id;
            }

            data.Payments = [];
            foreach (var payment in bundle.Payments)
            {
                if (!orderMap.TryGetValue(payment.OrderId, out var mappedOrderId))
                {
                    skipped++;
                    AddWarning(warnings, $"Bỏ qua thanh toán {FormatImportMoney(payment.Amount)} chưa khớp được đơn hàng.");
                    continue;
                }

                payment.OrderId = mappedOrderId;
                payment.CustomerId = data.Orders.FirstOrDefault(order =>
                    string.Equals(order.Id, mappedOrderId, StringComparison.Ordinal))?.CustomerId ?? payment.CustomerId;
                data.Payments.Add(payment);
            }

            paymentCount = data.Payments.Count;
        }
        else
        {
            var existingCustomerIds = data.Customers
                .Select(customer => customer.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var customer in bundle.Customers)
            {
                if (!existingCustomerIds.Add(customer.Id))
                {
                    skipped++;
                    AddWarning(warnings, $"Bỏ qua khách hàng trùng mã “{customer.Name}”.");
                    continue;
                }

                data.Customers.Add(customer);
                customerCount++;
            }

            var existingPackageIds = data.Packages
                .Select(package => package.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var package in bundle.Packages)
            {
                if (!existingPackageIds.Add(package.Id))
                {
                    skipped++;
                    AddWarning(warnings, $"Bỏ qua gói dịch vụ trùng mã “{package.Name}”.");
                    continue;
                }

                data.Packages.Add(package);
                packageCount++;
            }

            var existingStaffIds = data.Staff
                .Select(member => member.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var member in bundle.Staff)
            {
                if (!existingStaffIds.Add(member.Id))
                {
                    skipped++;
                    AddWarning(warnings, $"Bỏ qua nhân sự trùng mã “{member.Name}”.");
                    continue;
                }

                data.Staff.Add(member);
                staffCount++;
            }

            var existingBookingIds = data.Bookings
                .Select(booking => booking.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var booking in bundle.Bookings)
            {
                if (!existingBookingIds.Add(booking.Id))
                {
                    skipped++;
                    AddWarning(warnings, $"Bỏ qua lịch chụp trùng mã “{booking.Date}”.");
                    continue;
                }

                data.Bookings.Add(booking);
                bookingCount++;
            }

            var existingOrderIds = data.Orders
                .Select(order => order.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var order in bundle.Orders)
            {
                if (!existingOrderIds.Add(order.Id))
                {
                    skipped++;
                    skippedOrderIds.Add(order.Id);
                    AddWarning(warnings, $"Bỏ qua đơn hàng trùng mã “{order.Code}”.");
                    continue;
                }

                data.Orders.Add(order);
                orderMap[order.Id] = order.Id;
                orderCount++;
            }

            var existingPaymentIds = data.Payments
                .Select(payment => payment.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var payment in bundle.Payments)
            {
                var sourceOrderId = payment.OrderId;
                if (skippedOrderIds.Contains(sourceOrderId))
                {
                    skipped++;
                    continue;
                }

                if (!orderMap.TryGetValue(sourceOrderId, out var mappedOrderId))
                {
                    skipped++;
                    AddWarning(warnings, $"Bỏ qua thanh toán {FormatImportMoney(payment.Amount)} chưa khớp được đơn hàng.");
                    continue;
                }

                if (!existingPaymentIds.Add(payment.Id))
                {
                    skipped++;
                    continue;
                }

                payment.OrderId = mappedOrderId;
                payment.CustomerId = data.Orders.FirstOrDefault(order =>
                    string.Equals(order.Id, mappedOrderId, StringComparison.Ordinal))?.CustomerId ?? "";
                data.Payments.Add(payment);
                importedPaymentsByOrder[sourceOrderId] =
                    importedPaymentsByOrder.GetValueOrDefault(sourceOrderId) + payment.Amount;
                paymentCount++;
            }

            var existingContractIds = data.Contracts
                .Select(contract => contract.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var contract in bundle.Contracts)
            {
                if (!existingContractIds.Add(contract.Id))
                {
                    skipped++;
                    AddWarning(warnings, $"Bỏ qua hợp đồng trùng mã “{contract.Code}”.");
                    continue;
                }

                data.Contracts.Add(contract);
                contractCount++;
            }

            var existingExpenseIds = data.Expenses
                .Select(expense => expense.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var expense in bundle.Expenses)
            {
                if (!existingExpenseIds.Add(expense.Id))
                {
                    skipped++;
                    AddWarning(warnings, "Bỏ qua một khoản chi trùng mã.");
                    continue;
                }

                data.Expenses.Add(expense);
                expenseCount++;
            }

            var existingPayrollIds = data.Payrolls
                .Select(payroll => payroll.Id)
                .ToHashSet(StringComparer.Ordinal);
            var existingPayrollKeys = data.Payrolls
                .Select(payroll => payroll.StaffId + "|" + payroll.Month)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var payroll in bundle.Payrolls)
            {
                if (!existingPayrollIds.Add(payroll.Id) ||
                    !existingPayrollKeys.Add(payroll.StaffId + "|" + payroll.Month))
                {
                    skipped++;
                    AddWarning(warnings, $"Bỏ qua bảng lương trùng tháng {payroll.Month}.");
                    continue;
                }

                data.Payrolls.Add(payroll);
                payrollCount++;
            }

            var existingAttendanceIds = data.Attendance
                .Select(record => record.Id)
                .ToHashSet(StringComparer.Ordinal);
            var existingAttendanceKeys = data.Attendance
                .Select(record => record.StaffId + "|" + record.Date)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var record in bundle.Attendance)
            {
                if (!existingAttendanceIds.Add(record.Id) ||
                    !existingAttendanceKeys.Add(record.StaffId + "|" + record.Date))
                {
                    skipped++;
                    continue;
                }

                data.Attendance.Add(record);
                attendanceCount++;
            }

            var existingOutfitIds = data.Outfits
                .Select(outfit => outfit.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var outfit in bundle.Outfits)
            {
                if (!existingOutfitIds.Add(outfit.Id))
                {
                    skipped++;
                    AddWarning(warnings, $"Bỏ qua trang phục trùng mã “{outfit.Name}”.");
                    continue;
                }

                data.Outfits.Add(outfit);
                outfitCount++;
            }

            var existingConceptIds = data.Concepts
                .Select(concept => concept.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var concept in bundle.Concepts)
            {
                if (!existingConceptIds.Add(concept.Id))
                {
                    skipped++;
                    AddWarning(warnings, $"Bỏ qua concept trùng mã “{concept.Name}”.");
                    continue;
                }

                data.Concepts.Add(concept);
                conceptCount++;
            }

            var existingLoanIds = data.OutfitLoans
                .Select(loan => loan.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var loan in bundle.Loans)
            {
                if (!existingLoanIds.Add(loan.Id))
                {
                    skipped++;
                    AddWarning(warnings, "Bỏ qua một phiếu cho thuê trùng mã.");
                    continue;
                }

                data.OutfitLoans.Add(loan);
                loanCount++;
            }

            var existingNotificationIds = data.Notifications
                .Select(notification => notification.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var notification in bundle.Notifications)
            {
                if (!existingNotificationIds.Add(notification.Id))
                {
                    skipped++;
                    continue;
                }

                data.Notifications.Add(notification);
                notificationCount++;
            }
        }

        if (replaceExisting)
        {
            foreach (var payment in data.Payments)
            {
                importedPaymentsByOrder[payment.OrderId] =
                    importedPaymentsByOrder.GetValueOrDefault(payment.OrderId) + payment.Amount;
            }
        }

        // CRM chỉ lưu một con số paidAmount trên đơn hàng. Chuyển phần chưa
        // được ghi bằng payment riêng thành một khoản "số dư đầu kỳ" để
        // Payment vẫn là nguồn sự thật duy nhất.
        foreach (var opening in bundle.OpeningBalances)
        {
            if (!orderMap.TryGetValue(opening.SourceOrderId, out var mappedOrderId))
            {
                continue;
            }

            var alreadyImported = importedPaymentsByOrder.GetValueOrDefault(opening.SourceOrderId);
            var amount = Math.Max(0, opening.Amount - alreadyImported);
            if (amount <= 0)
            {
                continue;
            }

            data.Payments.Add(new Payment
            {
                Id = NewShortImportId(),
                Code = "",
                OrderId = mappedOrderId,
                CustomerId = opening.CustomerId,
                Amount = amount,
                Method = PaymentMethods.Other,
                PaidAt = opening.PaidAt,
                Note = "Số dư đầu kỳ từ CRM",
                ActorName = "CRM",
                CreatedAt = ParseTimestamp(opening.PaidAt, DateTimeOffset.UtcNow)
            });
        }

        EnsureSalesCodes(data);

        var customerIds = data.Customers.Select(customer => customer.Id).ToHashSet(StringComparer.Ordinal);
        var packageById = data.Packages
            .GroupBy(package => package.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var staffIds = data.Staff.Select(member => member.Id).ToHashSet(StringComparer.Ordinal);
        var albumById = data.Albums
            .GroupBy(album => album.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var usedCodes = new HashSet<string>(
            data.Bookings.Select(booking => booking.Code).Where(code => !string.IsNullOrWhiteSpace(code)),
            StringComparer.OrdinalIgnoreCase);
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var booking in data.Bookings)
        {
            if (!string.IsNullOrWhiteSpace(booking.CustomerId) && !customerIds.Contains(booking.CustomerId))
            {
                booking.CustomerId = "";
                AddWarning(warnings, $"Lịch chụp {booking.Date}: khách hàng không còn tồn tại, đã bỏ liên kết.");
            }

            if (!string.IsNullOrWhiteSpace(booking.PackageId) &&
                packageById.TryGetValue(booking.PackageId, out var package))
            {
                if (booking.Price <= 0)
                {
                    booking.Price = package.Price;
                }

                if (booking.Hours <= 0)
                {
                    booking.Hours = package.Hours;
                }
            }
            else if (!string.IsNullOrWhiteSpace(booking.PackageId))
            {
                booking.PackageId = "";
            }

            if (!string.IsNullOrWhiteSpace(booking.PhotographerId) &&
                !staffIds.Contains(booking.PhotographerId))
            {
                booking.PhotographerId = "";
            }

            if (!string.IsNullOrWhiteSpace(booking.AlbumId) &&
                albumById.TryGetValue(booking.AlbumId, out var album))
            {
                var customer = data.Customers.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, booking.CustomerId, StringComparison.Ordinal));
                LinkBookingAlbum(album, booking, customer);
            }
            else if (!string.IsNullOrWhiteSpace(booking.AlbumId))
            {
                booking.AlbumId = "";
            }

            if (string.IsNullOrWhiteSpace(booking.Code) || !seenCodes.Add(booking.Code))
            {
                booking.Code = NextBookingCode(usedCodes);
                seenCodes.Add(booking.Code);
            }

            if (booking.StatusHistory.Count == 0)
            {
                booking.StatusHistory.Add(new BookingStatusEntry
                {
                    Status = booking.Status,
                    ActorName = "CRM",
                    ChangedAt = booking.CreatedAt
                });
            }
        }

        // Keep existing album links when their customer was part of the import;
        // otherwise clear the dangling reference so the UI stays truthful.
        var clearedAlbums = 0;
        foreach (var album in data.Albums)
        {
            if (!string.IsNullOrWhiteSpace(album.CustomerId) && !customerIds.Contains(album.CustomerId))
            {
                album.CustomerId = "";
                clearedAlbums++;
            }
        }

        if (clearedAlbums > 0)
        {
            AddWarning(warnings, $"{clearedAlbums} album đã bỏ liên kết khách hàng vì không còn trong dữ liệu nhập.");
        }

        foreach (var member in data.Staff)
        {
            if (!string.IsNullOrWhiteSpace(member.UserId) &&
                !data.Users.Any(user => string.Equals(user.Id, member.UserId, StringComparison.Ordinal)))
            {
                member.UserId = "";
            }
        }

        if (actor is not null)
        {
            StudioAudit.Add(
                data,
                actor,
                "crm",
                "import",
                replaceExisting
                    ? "đã thay thế dữ liệu CRM từ bản sao lưu"
                    : "đã nhập dữ liệu CRM từ bản sao lưu",
                "",
                [
                    $"{customerCount} khách hàng",
                    $"{packageCount} gói dịch vụ",
                    $"{staffCount} nhân sự",
                    $"{bookingCount} lịch chụp",
                    orderCount > 0 ? $"{orderCount} đơn hàng" : "",
                    paymentCount > 0 ? $"{paymentCount} thanh toán" : "",
                    contractCount > 0 ? $"{contractCount} hợp đồng" : "",
                    expenseCount > 0 ? $"{expenseCount} chi phí" : "",
                    payrollCount > 0 ? $"{payrollCount} bảng lương" : "",
                    attendanceCount > 0 ? $"{attendanceCount} bản ghi chấm công" : "",
                    outfitCount > 0 ? $"{outfitCount} trang phục" : "",
                    conceptCount > 0 ? $"{conceptCount} concept" : "",
                    loanCount > 0 ? $"{loanCount} phiếu cho thuê" : "",
                    notificationCount > 0 ? $"{notificationCount} thông báo" : "",
                    skipped > 0 ? $"{skipped} mục bị bỏ qua" : ""
                ]);
        }

        return new ImportCrmResultDto(
            dryRun,
            customerCount,
            packageCount,
            staffCount,
            bookingCount,
            orderCount,
            paymentCount,
            contractCount,
            expenseCount,
            payrollCount,
            attendanceCount,
            outfitCount,
            conceptCount,
            loanCount,
            notificationCount,
            skipped,
            warnings.Take(80).ToList());
    }

    // --------------------------------------------------------------- helpers

    private static void ApplyStatusChange(
        Booking booking,
        string status,
        StudioUser actor,
        List<string> changes)
    {
        if (string.Equals(booking.Status, status, StringComparison.Ordinal))
        {
            return;
        }

        var previous = StatusLabel(booking.Status);
        booking.Status = status;
        booking.StatusHistory.Add(new BookingStatusEntry
        {
            Status = status,
            ActorName = ActorName(actor),
            ChangedAt = DateTimeOffset.UtcNow
        });
        changes.Add($"Trạng thái: {previous} → {StatusLabel(status)}");
    }

    private static void LinkBookingAlbum(Album album, Booking booking, Customer? customer)
    {
        if (string.IsNullOrWhiteSpace(album.CustomerId) && customer is not null)
        {
            album.CustomerId = customer.Id;
        }

        if (string.IsNullOrWhiteSpace(album.ClientName) && customer is not null)
        {
            album.ClientName = customer.Name;
        }

        if (string.IsNullOrWhiteSpace(album.EventDate))
        {
            album.EventDate = booking.Date;
        }
    }

    private static void LinkStaffUser(AppData data, StaffMember member, string? userId)
    {
        var nextUserId = userId?.Trim() ?? "";
        if (nextUserId.Length == 0)
        {
            foreach (var linked in data.Users)
            {
                if (string.Equals(linked.StaffId, member.Id, StringComparison.Ordinal))
                {
                    linked.StaffId = "";
                }
            }

            member.UserId = "";
            return;
        }

        foreach (var candidate in data.Users)
        {
            if (!string.Equals(candidate.Id, nextUserId, StringComparison.Ordinal) &&
                string.Equals(candidate.StaffId, member.Id, StringComparison.Ordinal))
            {
                candidate.StaffId = "";
            }
        }

        var user = data.Users.FirstOrDefault(candidate =>
                       string.Equals(candidate.Id, nextUserId, StringComparison.OrdinalIgnoreCase))
                   ?? throw new ApiException(400, "Tài khoản liên kết không còn tồn tại.", "invalid_user");
        if (!string.IsNullOrWhiteSpace(user.StaffId) &&
            !string.Equals(user.StaffId, member.Id, StringComparison.Ordinal))
        {
            throw new ApiException(
                409,
                "Tài khoản này đã được liên kết với một hồ sơ nhân sự khác.",
                "user_linked");
        }

        var other = data.Staff.FirstOrDefault(candidate =>
            !string.Equals(candidate.Id, member.Id, StringComparison.Ordinal) &&
            string.Equals(candidate.UserId, user.Id, StringComparison.Ordinal));
        if (other is not null)
        {
            throw new ApiException(
                409,
                $"Tài khoản này đã được liên kết với nhân sự “{other.Name}”.",
                "user_linked");
        }

        member.UserId = user.Id;
        user.StaffId = member.Id;
    }

    private static string UniqueImportId(
        string sourceId,
        HashSet<string> seen,
        string label,
        ImportBundle bundle)
    {
        var id = sourceId.Length > 0 ? sourceId : NewShortId();
        if (seen.Add(id))
        {
            return id;
        }

        var replacement = NewShortId();
        seen.Add(replacement);
        Warn(bundle, $"Mã {label} “{id}” bị trùng, đã tạo mã mới.");
        return replacement;
    }

    private static string CleanImportId(string? value)
    {
        var id = AlbumText.TrimTo(value, 40);
        return id;
    }

    private static int UnsupportedCount(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Array => element.GetArrayLength(),
            JsonValueKind.Object => element.EnumerateObject().Any(property =>
                property.Value.ValueKind == JsonValueKind.Object)
                ? element.EnumerateObject().Count(property =>
                    property.Value.ValueKind == JsonValueKind.Object)
                : element.TryGetProperty("id", out _) ||
                  element.TryGetProperty("title", out _) ||
                  element.TryGetProperty("name", out _) ||
                  element.TryGetProperty("date", out _)
                    ? 1
                    : 0,
            _ => 0
        };
    }

    private static void Warn(ImportBundle bundle, string message)
    {
        AddWarning(bundle.Warnings, message);
    }

    private static void AddWarning(List<string> warnings, string message)
    {
        if (warnings.Count >= 80 || warnings.Contains(message, StringComparer.Ordinal))
        {
            return;
        }

        warnings.Add(message);
    }

    private static DateTimeOffset ParseTimestamp(string? value, DateTimeOffset fallback)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : fallback;
    }

    private static string Today() =>
        DateTimeOffset.UtcNow
            .ToOffset(TimeSpan.FromHours(7))
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string ActorName(StudioUser? user)
    {
        if (user is null)
        {
            return "Studio";
        }

        return string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;
    }

    private static string StatusLabel(string status) => status switch
    {
        BookingStatuses.Pending => "Chờ xác nhận",
        BookingStatuses.Confirmed => "Đã xác nhận",
        BookingStatuses.Shooting => "Đang chụp",
        BookingStatuses.Editing => "Đang chỉnh sửa",
        BookingStatuses.Delivered => "Đã giao",
        BookingStatuses.Cancelled => "Đã huỷ",
        _ => status
    };

    private static string RequireNameOrThrow(string? value, string field, string code)
    {
        var name = AlbumText.TrimTo(value, 120);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ApiException(400, $"{field} không được để trống.", code);
        }

        return name;
    }

    private static string NormalizeCustomerTypeOrThrow(string? value)
    {
        var type = (value ?? "").Trim().ToLowerInvariant();
        return type switch
        {
            "" or "individual" => "individual",
            "company" => "company",
            _ => throw new ApiException(400, "Loại khách hàng không hợp lệ.", "invalid_customer_type")
        };
    }

    private static string NormalizeEmailOrThrow(string? value)
    {
        var email = AlbumText.TrimTo(value, 160);
        if (email.Length > 0 && !MailAddress.TryCreate(email, out _))
        {
            throw new ApiException(400, "Email chưa đúng định dạng.", "invalid_email");
        }

        return email;
    }

    private static string NormalizeStaffRole(string? value)
    {
        var role = AlbumText.TrimTo(value, 40).Trim().ToLowerInvariant();
        return role.Length == 0 ? "photographer" : role;
    }

    private static string NormalizeIncludes(string? value)
    {
        var raw = AlbumText.TrimTo(value, 4000);
        if (raw.Length == 0)
        {
            return "";
        }

        var parts = raw
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length > 0);
        return AlbumText.TrimTo(string.Join("\n", parts), 4000);
    }

    private static decimal NormalizeHoursOrThrow(decimal value)
    {
        if (value is < 0 or > 1000)
        {
            throw new ApiException(400, "Số giờ không hợp lệ.", "invalid_hours");
        }

        return value;
    }

    private static string RequireDateOrThrow(string? value)
    {
        var date = AlbumText.NormalizeEventDate(value);
        if (string.IsNullOrWhiteSpace(date))
        {
            throw new ApiException(400, "Ngày chụp chưa đúng định dạng.", "invalid_date");
        }

        return date;
    }

    private static string NormalizeClockOrThrow(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "09:00";
        }

        if (!TimeOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var time))
        {
            throw new ApiException(400, "Giờ bắt đầu chưa đúng định dạng.", "invalid_time");
        }

        return time.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private static string NormalizeClockOrDefault(string? value)
    {
        return TimeOnly.TryParse((value ?? "").Trim(), CultureInfo.InvariantCulture, out var time)
            ? time.ToString("HH:mm", CultureInfo.InvariantCulture)
            : "09:00";
    }

    private static string NormalizeClockOrEmpty(string? value)
    {
        return TimeOnly.TryParse((value ?? "").Trim(), CultureInfo.InvariantCulture, out var time)
            ? time.ToString("HH:mm", CultureInfo.InvariantCulture)
            : "";
    }

    private static string NormalizeImportMonth(string? value)
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

        return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            : "";
    }

    private static string NormalizeBookingStatusOrThrow(string? value)
    {
        var status = (value ?? "").Trim().ToLowerInvariant();
        if (status.Length == 0)
        {
            return BookingStatuses.Pending;
        }

        if (!BookingStatuses.IsKnown(status))
        {
            throw new ApiException(400, "Trạng thái lịch chụp không hợp lệ.", "invalid_booking_status");
        }

        return status;
    }

    private static Customer? ResolveCustomer(AppData data, string? customerId)
    {
        var id = customerId?.Trim() ?? "";
        if (id.Length == 0)
        {
            return null;
        }

        return data.Customers.FirstOrDefault(customer =>
                   string.Equals(customer.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? throw new ApiException(400, "Khách hàng không còn tồn tại.", "invalid_customer");
    }

    private static ServicePackage? ResolvePackage(AppData data, string? packageId)
    {
        var id = packageId?.Trim() ?? "";
        if (id.Length == 0)
        {
            return null;
        }

        return data.Packages.FirstOrDefault(package =>
                   string.Equals(package.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? throw new ApiException(400, "Gói dịch vụ không còn tồn tại.", "invalid_package");
    }

    private static StaffMember? ResolveStaff(AppData data, string? staffId)
    {
        var id = staffId?.Trim() ?? "";
        if (id.Length == 0)
        {
            return null;
        }

        return data.Staff.FirstOrDefault(member =>
                   string.Equals(member.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? throw new ApiException(400, "Nhân sự không còn tồn tại.", "invalid_staff");
    }

    private static Album? ResolveAlbum(AppData data, string? albumId)
    {
        var id = albumId?.Trim() ?? "";
        if (id.Length == 0)
        {
            return null;
        }

        return data.Albums.FirstOrDefault(album =>
                   string.Equals(album.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? throw new ApiException(400, "Album không còn tồn tại.", "invalid_album");
    }

    private static Customer FindCustomer(AppData data, string id) =>
        data.Customers.FirstOrDefault(customer =>
            string.Equals(customer.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new ApiException(404, "Không tìm thấy khách hàng.", "customer_not_found");

    private static ServicePackage FindPackage(AppData data, string id) =>
        data.Packages.FirstOrDefault(package =>
            string.Equals(package.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new ApiException(404, "Không tìm thấy gói dịch vụ.", "package_not_found");

    private static StaffMember FindStaff(AppData data, string id) =>
        data.Staff.FirstOrDefault(member =>
            string.Equals(member.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new ApiException(404, "Không tìm thấy nhân sự.", "staff_not_found");

    private static Booking FindBooking(AppData data, string id) =>
        data.Bookings.FirstOrDefault(booking =>
            string.Equals(booking.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new ApiException(404, "Không tìm thấy lịch chụp.", "booking_not_found");

    private static string NewShortId() => Guid.NewGuid().ToString("N")[..12];

    private static string NewShortImportId() => Guid.NewGuid().ToString("N")[..12];

    private static string FormatImportMoney(decimal value) =>
        $"{value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đ";

    private static void EnsureSalesCodes(AppData data)
    {
        data.Orders ??= [];
        data.Payments ??= [];
        data.Contracts ??= [];

        var orderUsed = new HashSet<string>(
            data.Orders.Select(order => order.Code).Where(code => !string.IsNullOrWhiteSpace(code)),
            StringComparer.OrdinalIgnoreCase);
        var orderSequence = 1;
        foreach (var code in orderUsed)
        {
            if (code.StartsWith("DH-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(code.AsSpan(3), out var number) &&
                number >= orderSequence)
            {
                orderSequence = number + 1;
            }
        }

        var orderSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var order in data.Orders)
        {
            if (string.IsNullOrWhiteSpace(order.Code) || !orderSeen.Add(order.Code))
            {
                order.Code = NextImportCode("DH-", orderUsed, ref orderSequence);
                orderSeen.Add(order.Code);
            }
        }

        data.OrderSequence = Math.Max(data.OrderSequence, orderSequence);

        var paymentUsed = new HashSet<string>(
            data.Payments.Select(payment => payment.Code).Where(code => !string.IsNullOrWhiteSpace(code)),
            StringComparer.OrdinalIgnoreCase);
        var paymentSequence = 1;
        foreach (var code in paymentUsed)
        {
            if (code.StartsWith("PT-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(code.AsSpan(3), out var number) &&
                number >= paymentSequence)
            {
                paymentSequence = number + 1;
            }
        }

        var paymentSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var payment in data.Payments)
        {
            if (string.IsNullOrWhiteSpace(payment.Code) || !paymentSeen.Add(payment.Code))
            {
                payment.Code = NextImportCode("PT-", paymentUsed, ref paymentSequence);
                paymentSeen.Add(payment.Code);
            }
        }

        data.PaymentSequence = Math.Max(data.PaymentSequence, paymentSequence);

        var contractUsed = new HashSet<string>(
            data.Contracts.Select(contract => contract.Code).Where(code => !string.IsNullOrWhiteSpace(code)),
            StringComparer.OrdinalIgnoreCase);
        var contractSequence = 1;
        foreach (var code in contractUsed)
        {
            if (code.StartsWith("HD-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(code.AsSpan(3), out var number) &&
                number >= contractSequence)
            {
                contractSequence = number + 1;
            }
        }

        var contractSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var contract in data.Contracts)
        {
            if (string.IsNullOrWhiteSpace(contract.Code) || !contractSeen.Add(contract.Code))
            {
                contract.Code = NextImportCode("HD-", contractUsed, ref contractSequence);
                contractSeen.Add(contract.Code);
            }
        }

        data.ContractSequence = Math.Max(data.ContractSequence, contractSequence);
    }

    private static string NextImportCode(string prefix, HashSet<string> usedCodes, ref int sequence)
    {
        string candidate;
        do
        {
            candidate = $"{prefix}{sequence:0000}";
            sequence++;
        }
        while (!usedCodes.Add(candidate));

        return candidate;
    }

    private static string NextBookingCode(IReadOnlyCollection<Booking> bookings) =>
        NextBookingCode(new HashSet<string>(
            bookings.Select(booking => booking.Code).Where(code => !string.IsNullOrWhiteSpace(code)),
            StringComparer.OrdinalIgnoreCase));

    private static string NextBookingCode(HashSet<string> usedCodes)
    {
        var number = 1;
        foreach (var code in usedCodes)
        {
            if (code.StartsWith("BK-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(code.AsSpan(3), out var existing) &&
                existing >= number)
            {
                number = existing + 1;
            }
        }

        string candidate;
        do
        {
            candidate = $"BK-{number:0000}";
            number++;
        }
        while (!usedCodes.Add(candidate));

        return candidate;
    }

    private sealed class ImportBundle
    {
        public List<Customer> Customers { get; } = [];
        public List<ServicePackage> Packages { get; } = [];
        public List<StaffMember> Staff { get; } = [];
        public List<Booking> Bookings { get; } = [];
        public List<Order> Orders { get; } = [];
        public List<Payment> Payments { get; } = [];
        public List<Contract> Contracts { get; } = [];
        public List<ExpenseEntry> Expenses { get; } = [];
        public List<PayrollEntry> Payrolls { get; } = [];
        public List<AttendanceRecord> Attendance { get; } = [];
        public List<Outfit> Outfits { get; } = [];
        public List<StudioConcept> Concepts { get; } = [];
        public List<OutfitLoan> Loans { get; } = [];
        public List<NotificationEntry> Notifications { get; } = [];
        public List<OpeningBalance> OpeningBalances { get; } = [];
        public List<string> Warnings { get; } = [];
        public int Skipped { get; set; }
    }

    private sealed record OpeningBalance(
        string SourceOrderId,
        decimal Amount,
        string PaidAt,
        string CustomerId);

    private sealed class CrmBackup
    {
        [JsonConverter(typeof(SingleOrArrayConverter<CrmCustomer>))]
        public List<CrmCustomer>? Customers { get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<CrmPackage>))]
        public List<CrmPackage>? Packages { get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<CrmStaff>))]
        public List<CrmStaff>? Staff { get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<CrmBooking>))]
        public List<CrmBooking>? Bookings { get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<CrmOrder>))]
        public List<CrmOrder>? Orders { get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<CrmPayment>))]
        public List<CrmPayment>? Payments { get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<CrmContract>))]
        public List<CrmContract>? Contracts { get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<CrmExpense>))]
        public List<CrmExpense>? Expenses { get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<CrmPayroll>))]
        public List<CrmPayroll>? Payrolls { get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<CrmAttendance>))]
        public List<CrmAttendance>? Attendance { get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<CrmOutfit>))]
        public List<CrmOutfit>? Outfits { get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<CrmConcept>))]
        public List<CrmConcept>? Concepts { get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<CrmLoan>))]
        public List<CrmLoan>? Loans { get; set; }

        [JsonConverter(typeof(SingleOrArrayConverter<CrmNotification>))]
        public List<CrmNotification>? Notifications { get; set; }
    }

    /// <summary>
    /// CRM lưu một số danh sách dưới dạng một object đơn lẻ thay vì mảng.
    /// Chấp nhận cả hai dạng để bản sao lưu cũ vẫn nhập được.
    /// </summary>
    private sealed class SingleOrArrayConverter<T> : JsonConverter<List<T>>
    {
        public override List<T>? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return [];
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                return JsonSerializer.Deserialize<List<T>>(ref reader, options) ?? [];
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                var item = JsonSerializer.Deserialize<T>(ref reader, options);
                return item is null ? [] : [item];
            }

            throw new JsonException("Dữ liệu CRM phải là mảng hoặc object.");
        }

        public override void Write(
            Utf8JsonWriter writer,
            List<T> value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }

    private sealed class CrmCustomer
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? Source { get; set; }
        public string? Notes { get; set; }
        public bool? Active { get; set; }
        public string? CreatedAt { get; set; }
    }

    private sealed class CrmPackage
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public decimal? Price { get; set; }
        public decimal? Hours { get; set; }
        public bool? Active { get; set; }
        public string? Includes { get; set; }
        public string? Notes { get; set; }
        public string? CreatedAt { get; set; }
    }

    private sealed class CrmStaff
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Role { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool? Active { get; set; }
        public decimal? Salary { get; set; }
        public decimal? CommissionPct { get; set; }
        public string? PayMode { get; set; }
        public decimal? HourlyRate { get; set; }
        public string? Shift { get; set; }
        public string? Notes { get; set; }
        public string? CreatedAt { get; set; }
    }

    private sealed class CrmBooking
    {
        public string? Id { get; set; }
        public string? CustomerId { get; set; }
        public string? PackageId { get; set; }
        public string? Date { get; set; }
        public string? StartTime { get; set; }
        public decimal? Hours { get; set; }
        public string? Location { get; set; }
        public string? PhotographerId { get; set; }
        public string? Status { get; set; }
        public string? Notes { get; set; }
        public string? AlbumId { get; set; }
        public decimal? Price { get; set; }
        public string? PortalToken { get; set; }
        public CrmGallery? Gallery { get; set; }
        public string? CreatedAt { get; set; }
    }

    private sealed class CrmGallery
    {
        public string? DriveId { get; set; }
        public string? SourceUrl { get; set; }
        public string? Title { get; set; }
        public List<CrmGalleryPhoto>? Photos { get; set; }
    }

    private sealed class CrmOrder
    {
        public string? Id { get; set; }
        public string? Code { get; set; }
        public string? CustomerId { get; set; }
        public string? BookingId { get; set; }
        public List<CrmOrderItem>? Items { get; set; }
        public decimal? Discount { get; set; }
        public decimal? PaidAmount { get; set; }
        public string? Status { get; set; }
        public string? Notes { get; set; }
        public string? CreatedAt { get; set; }
    }

    private sealed class CrmOrderItem
    {
        public string? Name { get; set; }
        public decimal? Qty { get; set; }
        public decimal? Price { get; set; }
    }

    private sealed class CrmPayment
    {
        public string? Id { get; set; }
        public string? Ts { get; set; }
        public decimal? Amount { get; set; }
        public string? Method { get; set; }
        public string? Content { get; set; }
        public string? OrderId { get; set; }
        public string? Note { get; set; }
        public string? Bank { get; set; }
        public string? Gateway { get; set; }
    }

    private sealed class CrmContract
    {
        public string? Id { get; set; }
        public string? Code { get; set; }
        public string? CustomerId { get; set; }
        public string? BookingId { get; set; }
        public string? Service { get; set; }
        public decimal? Total { get; set; }
        public string? Status { get; set; }
        public string? Terms { get; set; }
        public List<CrmContractSchedule>? Schedule { get; set; }
        public string? CreatedAt { get; set; }
    }

    private sealed class CrmContractSchedule
    {
        public string? Label { get; set; }
        public decimal? Amount { get; set; }
        public string? DueDate { get; set; }
        public bool? Paid { get; set; }
        public string? PaidAt { get; set; }
    }

    private sealed class CrmGalleryPhoto
    {
        public string? Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
    }

    private sealed class CrmExpense
    {
        public string? Id { get; set; }
        public string? Date { get; set; }
        public string? Note { get; set; }
        public decimal? Amount { get; set; }
        public string? Category { get; set; }
    }

    private sealed class CrmPayroll
    {
        public string? Id { get; set; }
        public string? Month { get; set; }
        public string? StaffId { get; set; }
        public decimal? Base { get; set; }
        public decimal? Hours { get; set; }
        public decimal? CommissionBase { get; set; }
        public decimal? Commission { get; set; }
        public decimal? Total { get; set; }
        public bool? Paid { get; set; }
        public string? PaidDate { get; set; }
    }

    private sealed class CrmAttendance
    {
        public string? Id { get; set; }
        public string? StaffId { get; set; }
        public string? Date { get; set; }
        public string? In { get; set; }
        public string? Out { get; set; }
        public string? Note { get; set; }
        public string? CreatedAt { get; set; }
    }

    private sealed class CrmOutfit
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public int? Qty { get; set; }
        public string? Note { get; set; }
    }

    private sealed class CrmConcept
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Desc { get; set; }
        public decimal? Fee { get; set; }
        public string? Status { get; set; }
    }

    private sealed class CrmLoan
    {
        public string? Id { get; set; }
        public string? OutfitId { get; set; }
        public int? Qty { get; set; }
        public string? BorrowerType { get; set; }
        public string? CustomerId { get; set; }
        public string? StaffId { get; set; }
        public string? BookingId { get; set; }
        public string? BorrowDate { get; set; }
        public string? DueDate { get; set; }
        public string? ReturnDate { get; set; }
        public decimal? Deposit { get; set; }
        public string? Notes { get; set; }
        public string? CreatedAt { get; set; }
    }

    private sealed class CrmNotification
    {
        public string? Id { get; set; }
        public string? Ts { get; set; }
        public string? Type { get; set; }
        public string? Title { get; set; }
        public string? Body { get; set; }
        public bool? Read { get; set; }
    }
}
