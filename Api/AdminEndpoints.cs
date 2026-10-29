using System.Net.Mail;
using System.Text;
using MayAlbum.Models;
using MayAlbum.Services;

namespace MayAlbum.Api;

public static class AdminEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/admin")
            .AddEndpointFilter<AdminOnlyFilter>();
        var albums = new StudioPermissionFilter(StudioPermissions.Albums);

        group.MapGet("/albums", GetAlbums).AddEndpointFilter(albums);
        group.MapGet("/albums/{id}", GetAlbum).AddEndpointFilter(albums);
        group.MapPost("/drive/preview", PreviewDrive).AddEndpointFilter(albums);
        group.MapPost("/albums", CreateAlbum).AddEndpointFilter(albums);
        group.MapPatch("/albums/{id}", UpdateAlbum).AddEndpointFilter(albums);
        group.MapPost("/albums/{id}/refresh", RefreshAlbum).AddEndpointFilter(albums);
        group.MapPost("/albums/{id}/rotate-link", RotateLink).AddEndpointFilter(albums);
        group.MapDelete("/albums/{id}", DeleteAlbum).AddEndpointFilter(albums);
        group.MapGet("/albums/{id}/selections.csv", ExportSelections).AddEndpointFilter(albums);
        group.MapPost("/settings/logo", UploadLogo).AddEndpointFilter<StudioAdminFilter>().DisableAntiforgery();
        group.MapDelete("/settings/logo", (DataStore store) =>
        {
            store.Write(data => data.Settings.LogoKey = "");
            return Results.Ok(BuildSettingsDto(store));
        }).AddEndpointFilter<StudioAdminFilter>();
        group.MapPost("/settings/login-image", UploadLoginImage).AddEndpointFilter<StudioAdminFilter>().DisableAntiforgery();
        group.MapDelete("/settings/login-image", (DataStore store) =>
        {
            store.Write(data => data.Settings.LoginImageKey = "");
            return Results.Ok(BuildSettingsDto(store));
        }).AddEndpointFilter<StudioAdminFilter>();
        group.MapGet("/settings", GetSettings).AddEndpointFilter<StudioAdminFilter>();
        group.MapPut("/settings", SaveSettings).AddEndpointFilter<StudioAdminFilter>();
        group.MapGet("/users", GetUsers).AddEndpointFilter<StudioAdminFilter>();
        group.MapPost("/users", CreateUser).AddEndpointFilter<StudioAdminFilter>();
        group.MapPut("/users/{id}", UpdateUser).AddEndpointFilter<StudioAdminFilter>();
        group.MapPost("/users/{id}/password", ResetUserPassword).AddEndpointFilter<StudioAdminFilter>();
        BoardEndpoints.Map(group);
        CrmEndpoints.Map(group);
        SalesEndpoints.Map(group);
        OperationsEndpoints.Map(group);
    }

    private static IResult GetAlbums(DataStore store)
    {
        var albums = store.Read(data =>
            data.Albums
                .OrderByDescending(album => album.CreatedAt)
                .Select(ApiMappings.ToSummary)
                .ToList());

        return Results.Ok(albums);
    }

    private static IResult GetAlbum(string id, DataStore store)
    {
        var album = store.Read(data => ApiMappings.ToAdminAlbum(FindAdminAlbum(data, id)));
        return Results.Ok(album);
    }

    private static async Task<IResult> PreviewDrive(
        PreviewDriveRequest request,
        DataStore store,
        GoogleDriveService drive,
        CancellationToken cancellationToken)
    {
        var folder = await LoadFolderOrThrow(
            request.Url,
            request.IncludeSubfolders,
            store,
            drive,
            cancellationToken);

        if (folder.Photos.Count == 0)
        {
            throw new ApiException(
                400,
                request.IncludeSubfolders
                    ? "Không tìm thấy ảnh nào trong thư mục này. Hãy kiểm tra lại thư mục hoặc quyền chia sẻ."
                    : "Không tìm thấy ảnh ở cấp đầu tiên. Nếu ảnh nằm trong thư mục con, hãy bật “Bao gồm thư mục con”.",
                "empty_folder");
        }

        return Results.Ok(ApiMappings.ToDrivePreview(folder));
    }

    private static async Task<IResult> CreateAlbum(
        CreateAlbumRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions,
        GoogleDriveService drive,
        CancellationToken cancellationToken)
    {
        var actor = sessions.RequireUser(context);
        var customer = store.Read(data => ResolveCustomer(data, request.CustomerId));
        var folder = await LoadFolderOrThrow(
            request.DriveFolderUrl,
            request.IncludeSubfolders,
            store,
            drive,
            cancellationToken);

        if (folder.Photos.Count == 0)
        {
            throw new ApiException(
                400,
                "Thư mục này không có ảnh. Hãy chọn thư mục khác hoặc bật “Bao gồm thư mục con”.",
                "empty_folder");
        }

        var normalizedDate = NormalizeDateOrThrow(request.EventDate);
        var title = AlbumText.TrimTo(request.Title, 100);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = AlbumText.TrimTo(folder.FolderName, 100);
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Album mới";
        }

        var codes = AlbumText.CreateUniqueCodes(folder.Photos.Select(photo => photo.FileName));
        var photos = folder.Photos
            .Select((photo, index) => ApiMappings.ToPhoto(photo, codes[index]))
            .ToList();

        var cover = photos.FirstOrDefault(photo => photo.DriveFileId == request.CoverDriveFileId)
                    ?? photos[0];
        var now = DateTimeOffset.UtcNow;

        var created = store.Write(data =>
        {
            var shareToken = AlbumText.NewShareToken(
                title,
                candidate => data.Albums.Any(album =>
                    string.Equals(album.ShareToken, candidate, StringComparison.OrdinalIgnoreCase)));

            var album = new Album
            {
                Id = Guid.NewGuid().ToString("N"),
                ShareToken = shareToken,
                Title = title,
                ClientName = string.IsNullOrWhiteSpace(request.ClientName)
                    ? (customer?.Name ?? "")
                    : AlbumText.TrimTo(request.ClientName, 80),
                CustomerId = customer?.Id ?? "",
                EventDate = normalizedDate,
                Location = AlbumText.TrimTo(request.Location, 120),
                Description = AlbumText.TrimTo(request.Description, 1200),
                DriveFolderUrl = AlbumText.TrimTo(request.DriveFolderUrl, 2048),
                DriveFolderId = folder.FolderId,
                IncludeSubfolders = request.IncludeSubfolders,
                AllowFavorites = request.AllowFavorites,
                AllowDownloads = request.AllowDownloads,
                IsPublished = request.IsPublished,
                IsDemo = false,
                CoverPhotoId = cover.Id,
                CreatedAt = now,
                UpdatedAt = now,
                Photos = photos
            };

            data.Albums.Add(album);
            StudioAudit.Add(
                data,
                actor,
                "albums",
                "create",
                $"đã tạo album “{album.Title}”",
                album.Id,
                [
                    $"{photos.Count} ảnh",
                    string.IsNullOrWhiteSpace(customer?.Name) ? "" : $"Khách hàng: {customer!.Name}"
                ]);
            return ApiMappings.ToAdminAlbum(album);
        });

        return Results.Created($"/api/admin/albums/{created.Id}", created);
    }

    private static IResult UpdateAlbum(
        string id,
        UpdateAlbumRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        if (request.Title is not null && string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ApiException(400, "Tên album không được để trống.", "invalid_title");
        }

        var normalizedDate = request.EventDate is null ? null : NormalizeDateOrThrow(request.EventDate);

        var dto = store.Write(data =>
        {
            var album = FindAdminAlbum(data, id);
            var changes = new List<string>();

            if (request.Title is not null)
            {
                var nextTitle = AlbumText.TrimTo(request.Title, 100);
                if (!string.Equals(nextTitle, album.Title, StringComparison.Ordinal))
                {
                    changes.Add($"Tên: {album.Title} → {nextTitle}");
                }

                album.Title = nextTitle;
            }

            if (request.ClientName is not null)
            {
                var nextClient = AlbumText.TrimTo(request.ClientName, 80);
                if (!string.Equals(nextClient, album.ClientName, StringComparison.Ordinal))
                {
                    changes.Add($"Khách: {nextClient}");
                }

                album.ClientName = nextClient;
            }

            if (request.CustomerId is not null)
            {
                var customer = ResolveCustomer(data, request.CustomerId);
                if (!string.Equals(customer?.Id ?? "", album.CustomerId, StringComparison.Ordinal))
                {
                    changes.Add(customer is null ? "Bỏ liên kết khách hàng" : $"Liên kết khách hàng: {customer.Name}");
                }

                album.CustomerId = customer?.Id ?? "";
                if (customer is not null && string.IsNullOrWhiteSpace(album.ClientName))
                {
                    album.ClientName = customer.Name;
                }
            }

            if (normalizedDate is not null)
            {
                album.EventDate = normalizedDate;
            }

            if (request.Location is not null)
            {
                album.Location = AlbumText.TrimTo(request.Location, 120);
            }

            if (request.Description is not null)
            {
                album.Description = AlbumText.TrimTo(request.Description, 1200);
            }

            if (request.AllowFavorites is not null)
            {
                album.AllowFavorites = request.AllowFavorites.Value;
            }

            if (request.AllowDownloads is not null)
            {
                album.AllowDownloads = request.AllowDownloads.Value;
            }

            if (request.IsPublished is not null)
            {
                album.IsPublished = request.IsPublished.Value;
            }

            if (request.CoverPhotoId is not null)
            {
                if (string.IsNullOrWhiteSpace(request.CoverPhotoId))
                {
                    album.CoverPhotoId = album.Photos.FirstOrDefault()?.Id ?? "";
                }
                else
                {
                    var cover = album.Photos.FirstOrDefault(photo => photo.Id == request.CoverPhotoId);
                    if (cover is null)
                    {
                        throw new ApiException(400, "Ảnh bìa không thuộc album này.", "invalid_cover");
                    }

                    album.CoverPhotoId = cover.Id;
                }
            }

            album.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "albums",
                "update",
                $"đã cập nhật album “{album.Title}”",
                album.Id,
                changes);
            return ApiMappings.ToAdminAlbum(album);
        });

        return Results.Ok(dto);
    }

    private static async Task<IResult> RefreshAlbum(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions,
        GoogleDriveService drive,
        CancellationToken cancellationToken)
    {
        var actor = sessions.RequireUser(context);
        var info = store.Read(data =>
        {
            var album = FindAdminAlbum(data, id);
            return new RefreshInfo(
                album.IsDemo,
                album.DriveFolderId,
                album.DriveFolderUrl,
                album.IncludeSubfolders);
        });

        if (info.IsDemo)
        {
            throw new ApiException(400, "Album mẫu không kết nối với Google Drive.", "demo_album");
        }

        var source = !string.IsNullOrWhiteSpace(info.FolderId) ? info.FolderId : info.FolderUrl;
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ApiException(400, "Album này chưa có liên kết Google Drive.", "missing_drive_link");
        }

        var folder = await LoadFolderOrThrow(source, info.IncludeSubfolders, store, drive, cancellationToken);
        if (folder.Photos.Count == 0)
        {
            throw new ApiException(
                400,
                "Thư mục Drive hiện không còn ảnh. Album chưa được thay đổi.",
                "empty_folder");
        }

        var result = store.Write(data =>
        {
            var album = FindAdminAlbum(data, id);
            var existingByFileId = album.Photos
                .Where(photo => !string.IsNullOrWhiteSpace(photo.DriveFileId))
                .GroupBy(photo => photo.DriveFileId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var oldCount = album.Photos.Count;
            var oldCoverDriveId = album.Photos
                .FirstOrDefault(photo => photo.Id == album.CoverPhotoId)
                ?.DriveFileId;
            var usedCodes = new HashSet<string>(
                album.Photos.Select(photo => photo.Code).Where(code => !string.IsNullOrWhiteSpace(code)),
                StringComparer.OrdinalIgnoreCase);

            var refreshed = new List<Photo>(folder.Photos.Count);
            var matched = 0;
            foreach (var sourcePhoto in folder.Photos)
            {
                if (existingByFileId.TryGetValue(sourcePhoto.FileId, out var existing))
                {
                    matched++;
                    ApiMappings.UpdatePhotoFromDrive(existing, sourcePhoto);
                    if (string.IsNullOrWhiteSpace(existing.Code))
                    {
                        existing.Code = NextUniqueCode(sourcePhoto.FileName, usedCodes);
                    }

                    refreshed.Add(existing);
                }
                else
                {
                    var code = NextUniqueCode(sourcePhoto.FileName, usedCodes);
                    refreshed.Add(ApiMappings.ToPhoto(sourcePhoto, code));
                }
            }

            album.Photos = refreshed;
            var validIds = refreshed.Select(photo => photo.Id).ToHashSet(StringComparer.Ordinal);
            album.Selections.RemoveAll(selection => !validIds.Contains(selection.PhotoId));

            var preservedCover = refreshed.FirstOrDefault(photo =>
                !string.IsNullOrWhiteSpace(oldCoverDriveId) &&
                photo.DriveFileId == oldCoverDriveId);
            album.CoverPhotoId = (preservedCover ?? refreshed[0]).Id;
            album.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "albums",
                "refresh",
                $"đã đồng bộ Drive cho album “{album.Title}”",
                album.Id,
                [
                    $"Thêm {refreshed.Count - matched} ảnh",
                    $"Gỡ {oldCount - matched} ảnh"
                ]);

            return new
            {
                album = ApiMappings.ToAdminAlbum(album),
                added = refreshed.Count - matched,
                removed = oldCount - matched
            };
        });

        return Results.Ok(result);
    }

    private static IResult RotateLink(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var token = store.Write(data =>
        {
            var album = FindAdminAlbum(data, id);
            album.ShareToken = AlbumText.NewShareToken(
                album.Title,
                candidate => data.Albums.Any(other =>
                    !string.Equals(other.Id, album.Id, StringComparison.Ordinal) &&
                    string.Equals(other.ShareToken, candidate, StringComparison.OrdinalIgnoreCase)));
            album.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "albums",
                "share",
                $"đã đổi liên kết chia sẻ album “{album.Title}”",
                album.Id);
            return album.ShareToken;
        });

        return Results.Ok(new { shareToken = token });
    }

    private static IResult DeleteAlbum(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        store.Write(data =>
        {
            var album = FindAdminAlbum(data, id);
            var title = album.Title;
            data.Albums.Remove(album);
            foreach (var card in data.Board.Lists.SelectMany(list => list.Cards))
            {
                if (string.Equals(card.AlbumId, album.Id, StringComparison.Ordinal))
                {
                    card.AlbumId = "";
                }
            }

            foreach (var booking in data.Bookings)
            {
                if (string.Equals(booking.AlbumId, album.Id, StringComparison.Ordinal))
                {
                    booking.AlbumId = "";
                }
            }

            StudioAudit.Add(
                data,
                actor,
                "albums",
                "delete",
                $"đã xoá album “{title}”",
                id);
        });

        return Results.NoContent();
    }

    private static IResult ExportSelections(string id, DataStore store)
    {
        var payload = store.Read(data =>
        {
            var album = FindAdminAlbum(data, id);
            var selected = album.Selections
                .GroupBy(selection => selection.PhotoId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().SelectedAt, StringComparer.Ordinal);
            var photos = album.Photos
                .Where(photo => selected.ContainsKey(photo.Id))
                .OrderBy(photo => photo.Code, NaturalStringComparer.Instance)
                .ToList();

            var csv = new StringBuilder();
            csv.Append('\uFEFF');
            csv.AppendLine("Mã hình,Tên tệp,Thời điểm chọn,Khách đã gửi lúc,Ghi chú của khách");
            var submitted = album.SubmittedAt?.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm") ?? "";
            foreach (var photo in photos)
            {
                var selectedAt = selected[photo.Id]
                    .ToOffset(TimeSpan.FromHours(7))
                    .ToString("dd/MM/yyyy HH:mm");
                csv.Append(AlbumText.CsvCell(photo.Code)).Append(',')
                    .Append(AlbumText.CsvCell(photo.FileName)).Append(',')
                    .Append(AlbumText.CsvCell(selectedAt)).Append(',')
                    .Append(AlbumText.CsvCell(submitted)).Append(',')
                    .Append(AlbumText.CsvCell(album.ClientNote)).AppendLine();
            }

            return new CsvPayload(album.Title, csv.ToString());
        });

        var fileName = AlbumText.Slugify(payload.Title) + "-lua-chon.csv";
        return Results.File(
            Encoding.UTF8.GetBytes(payload.Content),
            "text/csv; charset=utf-8",
            fileName);
    }

    private static IResult GetUsers(HttpContext context, DataStore store, AdminSessionStore sessions)
    {
        var current = sessions.RequireUser(context);
        var users = store.Read(data => data.Users
            .OrderByDescending(user => user.Role == "admin")
            .ThenBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(user => ApiMappings.ToStudioUser(user, current.Id))
            .ToList());
        return Results.Ok(users);
    }

    private static IResult CreateUser(
        CreateStudioUserRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var username = NormalizeUsernameOrThrow(request.Username);
        var displayName = RequireDisplayName(request.DisplayName);
        var role = NormalizeRoleOrThrow(request.Role);
        var password = ValidatePasswordOrThrow(request.Password);
        var permissions = ResolvePermissions(role, request.Permissions, null, false, false);
        var staffId = request.StaffId?.Trim() ?? "";
        var mustChangePassword = request.MustChangePassword ?? true;
        var now = DateTimeOffset.UtcNow;

        var dto = store.Write(data =>
        {
            if (data.Users.Any(user => string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ApiException(409, "Tên đăng nhập này đã được sử dụng.", "username_taken");
            }

            var user = new StudioUser
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Username = username,
                DisplayName = displayName,
                Role = role,
                PasswordHash = PasswordHasher.Hash(password),
                IsActive = true,
                MustChangePassword = mustChangePassword,
                PermissionsConfigured = true,
                Permissions = permissions,
                CreatedAt = now,
                UpdatedAt = now
            };
            data.Users.Add(user);
            LinkStaff(data, user, staffId);
            StudioAudit.Add(
                data,
                actor,
                "accounts",
                "create",
                $"đã tạo tài khoản “{user.DisplayName}” ({user.Username})",
                user.Id,
                [
                    $"Vai trò: {(role == "admin" ? "Quản trị viên" : "Nhân viên")}",
                    role == "employee" ? $"{permissions.Count} quyền được cấp" : ""
                ]);
            return ApiMappings.ToStudioUser(user, "");
        });

        return Results.Created($"/api/admin/users/{dto.Id}", dto);
    }

    private static IResult UpdateUser(
        string id,
        UpdateStudioUserRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var current = sessions.RequireUser(context);
        var username = request.Username is null ? null : NormalizeUsernameOrThrow(request.Username);
        var displayName = request.DisplayName is null ? null : RequireDisplayName(request.DisplayName);
        var role = request.Role is null ? null : NormalizeRoleOrThrow(request.Role);

        var dto = store.Write(data =>
        {
            var user = FindUser(data, id);
            var isSelf = string.Equals(user.Id, current.Id, StringComparison.Ordinal);
            var roleChanged = role is not null && !string.Equals(role, user.Role, StringComparison.Ordinal);
            if (isSelf && roleChanged)
            {
                throw new ApiException(400, "Bạn không thể tự đổi vai trò của mình.", "cannot_change_own_role");
            }

            if (isSelf && request.IsActive is false)
            {
                throw new ApiException(400, "Bạn không thể tự vô hiệu hoá tài khoản đang đăng nhập.", "cannot_disable_self");
            }

            if (user.Role == "admin" &&
                user.IsActive &&
                (string.Equals(role, "employee", StringComparison.Ordinal) || request.IsActive is false) &&
                data.Users.Count(candidate => candidate.IsActive && candidate.Role == "admin") <= 1)
            {
                throw new ApiException(
                    400,
                    "Studio cần ít nhất một quản trị viên đang hoạt động.",
                    "last_admin");
            }

            if (username is not null &&
                data.Users.Any(candidate =>
                    !string.Equals(candidate.Id, user.Id, StringComparison.Ordinal) &&
                    string.Equals(candidate.Username, username, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ApiException(409, "Tên đăng nhập này đã được sử dụng.", "username_taken");
            }

            var changes = new List<string>();

            if (username is not null)
            {
                user.Username = username;
            }

            if (displayName is not null)
            {
                user.DisplayName = displayName;
            }

            if (role is not null)
            {
                user.Role = role;
            }

            if (request.Permissions is not null || roleChanged)
            {
                user.Permissions = ResolvePermissions(
                    user.Role,
                    request.Permissions,
                    user.Permissions,
                    user.PermissionsConfigured,
                    roleChanged);
                user.PermissionsConfigured = true;
                changes.Add(user.Role == "admin"
                    ? "Toàn quyền quản trị"
                    : $"{user.Permissions.Count} quyền được cấp");
            }

            if (request.StaffId is not null)
            {
                var staffId = request.StaffId.Trim();
                if (!string.Equals(staffId, user.StaffId, StringComparison.Ordinal))
                {
                    LinkStaff(data, user, staffId);
                    changes.Add(string.IsNullOrWhiteSpace(staffId)
                        ? "Bỏ liên kết hồ sơ nhân sự"
                        : $"Liên kết hồ sơ nhân sự: {FindStaff(data, staffId).Name}");
                }
            }

            if (request.IsActive is not null)
            {
                user.IsActive = request.IsActive.Value;
                changes.Add(request.IsActive.Value ? "Kích hoạt tài khoản" : "Vô hiệu hoá tài khoản");
            }

            if (request.MustChangePassword is not null)
            {
                user.MustChangePassword = request.MustChangePassword.Value;
                changes.Add(request.MustChangePassword.Value
                    ? "Yêu cầu đổi mật khẩu khi đăng nhập"
                    : "Cho phép dùng mật khẩu hiện tại");
            }

            user.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                current,
                "accounts",
                "update",
                $"đã cập nhật tài khoản “{user.DisplayName}” ({user.Username})",
                user.Id,
                changes);
            return ApiMappings.ToStudioUser(user, current.Id);
        });

        return Results.Ok(dto);
    }

    private static IResult ResetUserPassword(
        string id,
        ResetStudioUserPasswordRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var password = ValidatePasswordOrThrow(request.Password);
        store.Write(data =>
        {
            var user = FindUser(data, id);
            var isSelf = string.Equals(user.Id, actor.Id, StringComparison.Ordinal);
            user.PasswordHash = PasswordHasher.Hash(password);
            user.MustChangePassword = !isSelf;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "accounts",
                "password",
                $"đã đặt lại mật khẩu cho tài khoản “{user.DisplayName}” ({user.Username})",
                user.Id,
                [isSelf ? "Tự đặt lại mật khẩu" : "Buộc đổi mật khẩu ở lần đăng nhập tới"]);
        });

        return Results.NoContent();
    }

    private static IResult GetSettings(DataStore store)
    {
        return Results.Ok(BuildSettingsDto(store));
    }

    private static async Task<IResult> UploadLogo(IFormFile? file, HttpContext context, DataStore store, IObjectStorage storage)
    {
        if (file is null || file.Length == 0 || file.Length > 2 * 1024 * 1024)
            throw new ApiException(400, "Chọn ảnh logo tối đa 2 MB.", "invalid_logo");
        await using var memory = new MemoryStream();
        await file.CopyToAsync(memory, context.RequestAborted);
        var bytes = memory.ToArray();
        var type = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ? "image/png"
            : bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 ? "image/jpeg"
            : bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP" ? "image/webp" : "";
        if (type.Length == 0) throw new ApiException(400, "Chỉ nhận ảnh PNG, JPG hoặc WebP hợp lệ.", "invalid_logo");
        var key = "logo-" + Guid.NewGuid().ToString("N");
        memory.Position = 0;
        await storage.PutAsync(key, memory, type, context.RequestAborted);
        store.Write(data => { data.Settings.LogoKey = key; data.Settings.LogoContentType = type; });
        return Results.Ok(BuildSettingsDto(store));
    }

    private static async Task<IResult> UploadLoginImage(IFormFile? file, HttpContext context, DataStore store, IObjectStorage storage)
    {
        if (file is null || file.Length == 0 || file.Length > 2 * 1024 * 1024)
            throw new ApiException(400, "Chọn ảnh đăng nhập tối đa 2 MB.", "invalid_login-image");
        await using var memory = new MemoryStream();
        await file.CopyToAsync(memory, context.RequestAborted);
        var bytes = memory.ToArray();
        var type = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ? "image/png"
            : bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 ? "image/jpeg"
            : bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP" ? "image/webp" : "";
        if (type.Length == 0) throw new ApiException(400, "Chỉ nhận ảnh PNG, JPG hoặc WebP hợp lệ.", "invalid_login-image");
        var key = "login-image-" + Guid.NewGuid().ToString("N");
        memory.Position = 0;
        await storage.PutAsync(key, memory, type, context.RequestAborted);
        store.Write(data => { data.Settings.LoginImageKey = key; data.Settings.LoginImageContentType = type; });
        return Results.Ok(BuildSettingsDto(store));
    }

    private static IResult SaveSettings(SaveSettingsRequest request, HttpContext context, DataStore store, AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var studioName = AlbumText.TrimTo(request.StudioName, 60);
        if (string.IsNullOrWhiteSpace(studioName))
        {
            throw new ApiException(400, "Tên studio không được để trống.", "invalid_studio_name");
        }

        var contactEmail = AlbumText.TrimTo(request.ContactEmail, 160);
        if (!string.IsNullOrWhiteSpace(contactEmail) &&
            !MailAddress.TryCreate(contactEmail, out _))
        {
            throw new ApiException(400, "Email liên hệ chưa đúng định dạng.", "invalid_email");
        }

        if (request.Website is not null)
        {
            request.Website.LoginName = request.Website.LoginName?.Trim() ?? "";
            if (request.Website.LoginName.Length > 100) throw new ApiException(400, "Tên hiển thị trang đăng nhập tối đa 100 ký tự.", "invalid_website_content");
            request.Website.LoginQuote = request.Website.LoginQuote?.Trim() ?? "";
            if (request.Website.LoginQuote.Length > 600) throw new ApiException(400, "Câu giới thiệu trên ảnh tối đa 600 ký tự.", "invalid_website_content");
            request.Website.LoginTitle = request.Website.LoginTitle?.Trim() ?? "";
            if (request.Website.LoginTitle.Length > 160) throw new ApiException(400, "Tiêu đề chào mừng tối đa 160 ký tự.", "invalid_website_content");
            request.Website.LoadingName = request.Website.LoadingName?.Trim() ?? "";
            request.Website.LoadingText = request.Website.LoadingText?.Trim() ?? "";
            if (request.Website.LoadingName.Length > 60 || request.Website.LoadingText.Length > 160)
                throw new ApiException(400, "Tên màn hình tải tối đa 60 ký tự, lời nhắn tối đa 160 ký tự.", "invalid_loading_text");
            request.Website.BrandName = request.Website.BrandName?.Trim() ?? "";
            if (request.Website.BrandName.Length > 60)
                throw new ApiException(400, "Nội dung tên hiển thị trên logo quá dài (tối đa 60 ký tự).", "invalid_website_content");
            request.Website.BrandLabel = request.Website.BrandLabel?.Trim() ?? "";
            if (request.Website.BrandLabel.Length > 30)
                throw new ApiException(400, "Nội dung chữ nhỏ bên cạnh logo quá dài (tối đa 30 ký tự).", "invalid_website_content");
            request.Website.HowItWorksLabel = request.Website.HowItWorksLabel?.Trim() ?? "";
            if (request.Website.HowItWorksLabel.Length > 60)
                throw new ApiException(400, "Nội dung menu: cách hoạt động quá dài (tối đa 60 ký tự).", "invalid_website_content");
            request.Website.SampleAlbumsLabel = request.Website.SampleAlbumsLabel?.Trim() ?? "";
            if (request.Website.SampleAlbumsLabel.Length > 60)
                throw new ApiException(400, "Nội dung menu: album mẫu quá dài (tối đa 60 ký tự).", "invalid_website_content");
            request.Website.ForStudioLabel = request.Website.ForStudioLabel?.Trim() ?? "";
            if (request.Website.ForStudioLabel.Length > 60)
                throw new ApiException(400, "Nội dung menu: dành cho studio quá dài (tối đa 60 ký tự).", "invalid_website_content");
            request.Website.HeroTitle = request.Website.HeroTitle?.Trim() ?? "";
            if (request.Website.HeroTitle.Length > 160)
                throw new ApiException(400, "Nội dung tiêu đề giới thiệu trang chủ quá dài (tối đa 160 ký tự).", "invalid_website_content");
            request.Website.HeroDescription = request.Website.HeroDescription?.Trim() ?? "";
            if (request.Website.HeroDescription.Length > 1200)
                throw new ApiException(400, "Nội dung đoạn giới thiệu trang chủ quá dài (tối đa 1200 ký tự).", "invalid_website_content");
            request.Website.FooterDescription = request.Website.FooterDescription?.Trim() ?? "";
            if (request.Website.FooterDescription.Length > 600)
                throw new ApiException(400, "Nội dung giới thiệu ở chân trang quá dài (tối đa 600 ký tự).", "invalid_website_content");
            request.Website.FooterPrivacy = request.Website.FooterPrivacy?.Trim() ?? "";
            if (request.Website.FooterPrivacy.Length > 600)
                throw new ApiException(400, "Nội dung dòng ghi chú riêng tư ở chân trang quá dài (tối đa 600 ký tự).", "invalid_website_content");
            if (string.IsNullOrWhiteSpace(request.Website.BrandName))
                throw new ApiException(400, "Tên hiển thị trên logo không được để trống.", "invalid_brand_name");
        }

        var newKey = request.GoogleApiKey?.Trim();
        if (newKey is { Length: > 500 })
        {
            throw new ApiException(400, "Google API key quá dài.", "invalid_api_key");
        }

        store.Write(data =>
        {
            data.Settings.StudioName = studioName;
            data.Settings.ContactEmail = contactEmail;
            if (request.Website is not null) data.Settings.Website = request.Website;

            if (request.ClearGoogleApiKey)
            {
                data.Settings.GoogleApiKey = "";
            }
            else if (!string.IsNullOrWhiteSpace(newKey))
            {
                data.Settings.GoogleApiKey = newKey;
            }

            StudioAudit.Add(
                data,
                actor,
                "settings",
                "update",
                "đã cập nhật cài đặt studio",
                "",
                [
                    $"Tên studio: {studioName}",
                    request.ClearGoogleApiKey
                        ? "Đã xoá Google API key"
                        : (!string.IsNullOrWhiteSpace(newKey) ? "Đã cập nhật Google API key" : "")
                ]);
        });

        return Results.Ok(BuildSettingsDto(store));
    }

    private static AdminSettingsDto BuildSettingsDto(DataStore store)
    {
        var settings = store.Read(data => new StudioSettings
        {
            StudioName = data.Settings.StudioName,
            ContactEmail = data.Settings.ContactEmail,
            LoginImageKey = data.Settings.LoginImageKey,
            LoginImageContentType = data.Settings.LoginImageContentType,
            LogoKey = data.Settings.LogoKey,
            LogoContentType = data.Settings.LogoContentType,
            Website = data.Settings.Website
        });
        return ApiMappings.ToSettings(settings, store.GetEffectiveGoogleApiKey());
    }

    private static async Task<DriveFolderResult> LoadFolderOrThrow(
        string? url,
        bool includeSubfolders,
        DataStore store,
        GoogleDriveService drive,
        CancellationToken cancellationToken)
    {
        try
        {
            return await drive.LoadFolderAsync(
                url,
                includeSubfolders,
                store.GetEffectiveGoogleApiKey(),
                cancellationToken);
        }
        catch (DriveException exception)
        {
            var status = exception.Code switch
            {
                "network_error" or "drive_timeout" => StatusCodes.Status503ServiceUnavailable,
                "drive_quota" => StatusCodes.Status429TooManyRequests,
                _ => StatusCodes.Status400BadRequest
            };
            throw new ApiException(status, exception.Message, exception.Code);
        }
    }

    private static string NormalizeDateOrThrow(string? value)
    {
        var normalized = AlbumText.NormalizeEventDate(value);
        if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(normalized))
        {
            throw new ApiException(400, "Ngày chụp chưa đúng định dạng.", "invalid_date");
        }

        return normalized;
    }

    private static string NextUniqueCode(string fileName, HashSet<string> usedCodes)
    {
        var baseCode = AlbumText.CleanCode(fileName);
        var code = baseCode;
        var counter = 2;
        while (!usedCodes.Add(code))
        {
            code = $"{baseCode}-{counter:00}";
            counter++;
        }

        return code;
    }

    private static Album FindAdminAlbum(AppData data, string id)
    {
        return data.Albums.FirstOrDefault(album =>
                   string.Equals(album.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? throw new ApiException(404, "Không tìm thấy album.", "album_not_found");
    }

    private static StudioUser FindUser(AppData data, string id)
    {
        return data.Users.FirstOrDefault(user =>
                   string.Equals(user.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? throw new ApiException(404, "Không tìm thấy tài khoản.", "user_not_found");
    }

    private static StaffMember FindStaff(AppData data, string id)
    {
        return data.Staff.FirstOrDefault(member =>
                   string.Equals(member.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? throw new ApiException(404, "Không tìm thấy hồ sơ nhân sự.", "staff_not_found");
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
               ?? throw new ApiException(400, "Khách hàng liên kết không còn tồn tại.", "invalid_customer");
    }

    private static List<string> ResolvePermissions(
        string role,
        List<string>? requested,
        List<string>? existing,
        bool configured,
        bool roleChanged)
    {
        if (string.Equals(role, "admin", StringComparison.Ordinal))
        {
            return [.. StudioPermissions.All];
        }

        if (requested is not null)
        {
            var unknown = requested
                .Where(permission => !StudioPermissions.IsKnown(permission))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (unknown.Count > 0)
            {
                throw new ApiException(
                    400,
                    $"Quyền không hợp lệ: {string.Join(", ", unknown)}.",
                    "invalid_permission");
            }

            return requested
                .Where(StudioPermissions.IsKnown)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        if (roleChanged || !configured)
        {
            return [.. StudioPermissions.EmployeeDefaults];
        }

        return (existing ?? [])
            .Where(StudioPermissions.IsKnown)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void LinkStaff(AppData data, StudioUser user, string? staffId)
    {
        var nextStaffId = staffId?.Trim() ?? "";
        foreach (var member in data.Staff)
        {
            if (string.Equals(member.UserId, user.Id, StringComparison.Ordinal) &&
                !string.Equals(member.Id, nextStaffId, StringComparison.Ordinal))
            {
                member.UserId = "";
            }
        }

        if (nextStaffId.Length == 0)
        {
            user.StaffId = "";
            return;
        }

        var staff = FindStaff(data, nextStaffId);
        if (!string.IsNullOrWhiteSpace(staff.UserId) &&
            !string.Equals(staff.UserId, user.Id, StringComparison.Ordinal))
        {
            throw new ApiException(
                409,
                "Hồ sơ nhân sự này đã được liên kết với một tài khoản khác.",
                "staff_linked");
        }

        var other = data.Users.FirstOrDefault(candidate =>
            !string.Equals(candidate.Id, user.Id, StringComparison.Ordinal) &&
            string.Equals(candidate.StaffId, staff.Id, StringComparison.Ordinal));
        if (other is not null)
        {
            throw new ApiException(
                409,
                $"Hồ sơ nhân sự này đã được liên kết với tài khoản “{other.Username}”.",
                "staff_linked");
        }

        staff.UserId = user.Id;
        user.StaffId = staff.Id;
    }

    private static string NormalizeUsernameOrThrow(string? value)
    {
        var username = (value ?? "").Trim().ToLowerInvariant();
        var valid = username.Length is >= 3 and <= 32 &&
                    char.IsAsciiLetterOrDigit(username[0]) &&
                    username.All(character =>
                        char.IsAsciiLetterOrDigit(character) ||
                        character is '.' or '_' or '-');
        if (!valid)
        {
            throw new ApiException(
                400,
                "Tên đăng nhập cần 3–32 ký tự, bắt đầu bằng chữ hoặc số và chỉ gồm chữ thường, số, dấu chấm, gạch ngang hoặc gạch dưới.",
                "invalid_username");
        }

        return username;
    }

    private static string RequireDisplayName(string? value)
    {
        var displayName = AlbumText.TrimTo(value, 60);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ApiException(400, "Tên hiển thị không được để trống.", "invalid_display_name");
        }

        return displayName;
    }

    private static string NormalizeRoleOrThrow(string? value)
    {
        var role = (value ?? "").Trim().ToLowerInvariant();
        if (role is not ("admin" or "employee"))
        {
            throw new ApiException(400, "Vai trò tài khoản không hợp lệ.", "invalid_role");
        }

        return role;
    }

    private static string ValidatePasswordOrThrow(string? value)
    {
        var password = value ?? "";
        if (password.Length is < 8 or > 128)
        {
            throw new ApiException(400, "Mật khẩu cần từ 8 đến 128 ký tự.", "invalid_password");
        }

        return password;
    }

    private sealed record RefreshInfo(
        bool IsDemo,
        string FolderId,
        string FolderUrl,
        bool IncludeSubfolders);

    private sealed record CsvPayload(string Title, string Content);
}
