using StudioCore.Models;
using StudioCore.Services;

namespace StudioCore.Api;

public static class AuthEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest request, HttpContext context, AdminSessionStore sessions, DataStore store) =>
        {
            var username = (request.Username ?? "").Trim().ToLowerInvariant();
            var password = request.Password ?? "";
            StudioUser? user;

            if (!string.IsNullOrWhiteSpace(username))
            {
                user = store.Read(data => data.Users.FirstOrDefault(candidate =>
                    string.Equals(candidate.Username, username, StringComparison.OrdinalIgnoreCase)));
                if (user is null ||
                    !user.IsActive ||
                    !PasswordHasher.Verify(password, user.PasswordHash))
                {
                    await Task.Delay(320);
                    throw new ApiException(
                        401,
                        "Tên đăng nhập hoặc mật khẩu chưa đúng. Vui lòng thử lại.",
                        "invalid_credentials");
                }
            }
            else if (!string.IsNullOrWhiteSpace(request.Code) && sessions.VerifyCode(request.Code))
            {
                // Keep the original access-code flow working for existing API
                // clients; it signs in the first active administrator.
                user = store.Read(data => data.Users.FirstOrDefault(candidate =>
                    candidate.IsActive && candidate.Role == "admin"));
                if (user is null)
                {
                    await Task.Delay(320);
                    throw new ApiException(401, "Chưa có tài khoản quản trị đang hoạt động.", "no_admin");
                }
            }
            else
            {
                await Task.Delay(320);
                throw new ApiException(
                    401,
                    "Tên đăng nhập hoặc mật khẩu chưa đúng. Vui lòng thử lại.",
                    "invalid_credentials");
            }

            var now = DateTimeOffset.UtcNow;
            store.Write(data =>
            {
                var target = data.Users.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, user.Id, StringComparison.Ordinal));
                if (target is not null)
                {
                    target.LastLoginAt = now;
                    target.UpdatedAt = now;
                }
            });

            sessions.SignIn(context, user);
            var studioName = store.Read(data => data.Settings.StudioName);
            return Results.Ok(new SessionDto(true, studioName, ApiMappings.ToSessionUser(user)));
        });

        group.MapGet("/session", (HttpContext context, AdminSessionStore sessions, DataStore store) =>
        {
            var user = sessions.GetCurrentUser(context);
            var studioName = store.Read(data => data.Settings.StudioName);
            return Results.Ok(new SessionDto(
                user is not null,
                studioName,
                user is null ? null : ApiMappings.ToSessionUser(user)));
        });

        group.MapPost("/change-password", (ChangePasswordRequest request, HttpContext context, AdminSessionStore sessions, DataStore store) =>
        {
            var current = sessions.RequireUser(context);
            var currentPassword = request.CurrentPassword ?? "";
            var newPassword = request.NewPassword ?? "";

            if (!PasswordHasher.Verify(currentPassword, current.PasswordHash))
            {
                throw new ApiException(400, "Mật khẩu hiện tại chưa đúng.", "invalid_current_password");
            }

            if (newPassword.Length is < 8 or > 128)
            {
                throw new ApiException(400, "Mật khẩu mới cần từ 8 đến 128 ký tự.", "invalid_password");
            }

            if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
            {
                throw new ApiException(400, "Mật khẩu mới cần khác mật khẩu hiện tại.", "password_unchanged");
            }

            var updated = store.Write(data =>
            {
                var user = data.Users.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, current.Id, StringComparison.Ordinal))
                    ?? throw new ApiException(401, "Phiên đăng nhập đã hết hạn.", "unauthorized");
                user.PasswordHash = PasswordHasher.Hash(newPassword);
                user.MustChangePassword = false;
                user.UpdatedAt = DateTimeOffset.UtcNow;
                StudioAudit.Add(data, user, "account", "password", "đã đổi mật khẩu đăng nhập", user.Id);
                return user;
            });

            var studioName = store.Read(data => data.Settings.StudioName);
            return Results.Ok(new SessionDto(true, studioName, ApiMappings.ToSessionUser(updated)));
        });

        group.MapPost("/logout", (HttpContext context, AdminSessionStore sessions) =>
        {
            sessions.SignOut(context);
            return Results.NoContent();
        });
    }
}
