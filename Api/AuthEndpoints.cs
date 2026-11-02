using Microsoft.AspNetCore.Authentication;
using StudioCore.Models;
using StudioCore.Persistence;
using StudioCore.Services;

namespace StudioCore.Api;

public static class AuthEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest request, HttpContext context, AdminSessionStore sessions, DataStore store, IConfiguration configuration) =>
        {
            var username = (request.Username ?? "").Trim().ToLowerInvariant();
            var password = request.Password ?? "";
            StudioUser? user;
            long loginRevocationVersion;

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
                if (!user.IsEmailVerified)
                    throw new ApiException(403, "Email chưa được xác thực. Hãy mở liên kết StudioCore đã gửi cho bạn.", "email_not_verified");
                loginRevocationVersion = sessions.CaptureRevocationVersion(context, user.Id);
            }
            else if (context.RequestServices.GetRequiredService<StudioCore.Persistence.TenantContext>().TenantId == Studio.MasterId && !string.IsNullOrWhiteSpace(request.Code) && sessions.VerifyCode(request.Code))
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
                loginRevocationVersion = sessions.CaptureRevocationVersion(context, user.Id);
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

            sessions.SignIn(context, user, loginRevocationVersion);
            var studioName = store.Read(data => data.Settings.StudioName);
            return Results.Ok(new SessionDto(true, studioName, ApiMappings.ToSessionUser(user), PlatformOrigin(context, configuration)));
        });

        group.MapGet("/session", (HttpContext context, AdminSessionStore sessions, DataStore store, IConfiguration configuration) =>
        {
            var user = sessions.GetCurrentUser(context);
            var studioName = store.Read(data => data.Settings.StudioName);
            return Results.Ok(new SessionDto(
                user is not null,
                studioName,
                user is null ? null : ApiMappings.ToSessionUser(user),
                PlatformOrigin(context, configuration)));
        });

        group.MapPost("/change-password", (ChangePasswordRequest request, HttpContext context, AdminSessionStore sessions, DataStore store, IConfiguration configuration) =>
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

            sessions.RevokeUserSessions(context, updated.Id);
            sessions.SignIn(context, updated);
            var studioName = store.Read(data => data.Settings.StudioName);
            return Results.Ok(new SessionDto(true, studioName, ApiMappings.ToSessionUser(updated), PlatformOrigin(context, configuration)));
        });

        group.MapPost("/logout", async (HttpContext context, AdminSessionStore sessions) =>
        {
            sessions.SignOut(context);
            await context.SignOutAsync(GoogleAuthEndpoints.PlatformCookie);
            return Results.NoContent();
        });

        group.MapPost("/verify-email", (AccountTokenRequest request, TenantContext tenant, SaasDatabase database) =>
        {
            var tokenHash = ValidateToken(request.Token);
            database.Mutate(tenant.TenantId, (db, _) =>
            {
                var token = db.Set<AccountToken>().SingleOrDefault(candidate =>
                    candidate.Purpose == AccountTokens.VerifyEmail && candidate.TokenHash == tokenHash);
                if (token is null || token.UsedAt is not null || token.ExpiresAt <= DateTimeOffset.UtcNow)
                    throw new ApiException(400, "Liên kết xác thực đã hết hạn hoặc đã được sử dụng.", "invalid_account_token");
                var user = db.Set<StudioUser>().SingleOrDefault(candidate => candidate.Id == token.UserId)
                    ?? throw new ApiException(400, "Liên kết xác thực không còn hợp lệ.", "invalid_account_token");
                user.IsEmailVerified = true;
                user.UpdatedAt = DateTimeOffset.UtcNow;
                token.UsedAt = DateTimeOffset.UtcNow;
                return 0;
            });
            return Results.Ok(new { Verified = true });
        }).RequireRateLimiting("account-lifecycle");

        group.MapPost("/verification-email", async (AccountEmailRequest request, TenantContext tenant, SaasDatabase database, AccountEmailSender emailSender) =>
        {
            if (!emailSender.IsConfigured)
                throw new ApiException(503, "Hệ thống chưa cấu hình gửi email.", "email_not_configured");
            var identifier = NormalizeIdentifier(request.Identifier);
            var issued = database.Mutate(tenant.TenantId, (db, studio) =>
            {
                var user = db.Set<StudioUser>().AsEnumerable().FirstOrDefault(candidate =>
                    string.Equals(candidate.Email, identifier, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.Username, identifier, StringComparison.OrdinalIgnoreCase));
                if (user is null || !user.IsActive || user.IsEmailVerified) return null;
                var now = DateTimeOffset.UtcNow;
                foreach (var old in db.Set<AccountToken>().Where(candidate => candidate.UserId == user.Id && candidate.Purpose == AccountTokens.VerifyEmail && candidate.UsedAt == null))
                    old.UsedAt = now;
                var raw = AccountTokens.NewToken();
                db.Add(new AccountToken { UserId = user.Id, Purpose = AccountTokens.VerifyEmail,
                    TokenHash = AccountTokens.Hash(raw), ExpiresAt = now.AddHours(24) });
                return new IssuedAccountToken(studio.Slug, user.Email, raw);
            });
            if (issued is not null)
            {
                var url = emailSender.PublicOrigin + $"/s/{issued.Slug}/admin/verify-email?token={issued.Token}";
                await emailSender.SendAsync(issued.Email, "Xác thực tài khoản StudioCore", $"Xác thực email trong 24 giờ tại:\n{url}");
            }
            return Results.Accepted(value: new { Sent = true, Token = emailSender.IsTestMode ? issued?.Token : null });
        }).RequireRateLimiting("account-lifecycle");

        group.MapPost("/forgot-password", async (AccountEmailRequest request, TenantContext tenant, SaasDatabase database, AccountEmailSender emailSender) =>
        {
            if (!emailSender.IsConfigured)
                throw new ApiException(503, "Hệ thống chưa cấu hình gửi email.", "email_not_configured");
            var identifier = NormalizeIdentifier(request.Identifier);
            var issued = database.Mutate(tenant.TenantId, (db, studio) =>
            {
                var user = db.Set<StudioUser>().AsEnumerable().FirstOrDefault(candidate => candidate.IsActive &&
                    (string.Equals(candidate.Email, identifier, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(candidate.Username, identifier, StringComparison.OrdinalIgnoreCase)));
                if (user is null || string.IsNullOrWhiteSpace(user.Email)) return null;
                var now = DateTimeOffset.UtcNow;
                foreach (var old in db.Set<AccountToken>().Where(candidate => candidate.UserId == user.Id && candidate.Purpose == AccountTokens.ResetPassword && candidate.UsedAt == null))
                    old.UsedAt = now;
                var raw = AccountTokens.NewToken();
                db.Add(new AccountToken { UserId = user.Id, Purpose = AccountTokens.ResetPassword,
                    TokenHash = AccountTokens.Hash(raw), ExpiresAt = now.AddMinutes(30) });
                return new IssuedAccountToken(studio.Slug, user.Email, raw);
            });
            if (issued is not null)
            {
                var url = emailSender.PublicOrigin + $"/s/{issued.Slug}/admin/reset-password?token={issued.Token}";
                await emailSender.SendAsync(issued.Email, "Đặt lại mật khẩu StudioCore", $"Liên kết đặt lại mật khẩu có hiệu lực 30 phút:\n{url}\n\nNếu bạn không yêu cầu, hãy bỏ qua email này.");
            }
            return Results.Accepted(value: new { Sent = true, Token = emailSender.IsTestMode ? issued?.Token : null });
        }).RequireRateLimiting("account-lifecycle");

        group.MapPost("/reset-password", (ResetPasswordWithTokenRequest request, HttpContext context, TenantContext tenant, SaasDatabase database, AdminSessionStore sessions) =>
        {
            var tokenHash = ValidateToken(request.Token);
            var password = request.NewPassword ?? "";
            if (password.Length is < 8 or > 128)
                throw new ApiException(400, "Mật khẩu mới cần từ 8 đến 128 ký tự.", "invalid_password");
            var userId = database.Mutate(tenant.TenantId, (db, _) =>
            {
                var token = db.Set<AccountToken>().SingleOrDefault(candidate =>
                    candidate.Purpose == AccountTokens.ResetPassword && candidate.TokenHash == tokenHash);
                if (token is null || token.UsedAt is not null || token.ExpiresAt <= DateTimeOffset.UtcNow)
                    throw new ApiException(400, "Liên kết đặt lại mật khẩu đã hết hạn hoặc đã được sử dụng.", "invalid_account_token");
                var user = db.Set<StudioUser>().SingleOrDefault(candidate => candidate.Id == token.UserId && candidate.IsActive)
                    ?? throw new ApiException(400, "Liên kết đặt lại mật khẩu không còn hợp lệ.", "invalid_account_token");
                var now = DateTimeOffset.UtcNow;
                user.PasswordHash = PasswordHasher.Hash(password);
                user.MustChangePassword = false;
                user.UpdatedAt = now;
                foreach (var active in db.Set<AccountToken>().Where(candidate => candidate.UserId == user.Id && candidate.Purpose == AccountTokens.ResetPassword && candidate.UsedAt == null))
                    active.UsedAt = now;
                return user.Id;
            });
            sessions.RevokeUserSessions(context, userId);
            return Results.Ok(new { Reset = true });
        }).RequireRateLimiting("account-lifecycle");
    }

    private static string PlatformOrigin(HttpContext context, IConfiguration configuration)
    {
        var configured = (configuration["Saas:PublicOrigin"] ?? "").Trim().TrimEnd('/');
        if (Uri.TryCreate(configured, UriKind.Absolute, out var origin) &&
            (origin.Scheme == Uri.UriSchemeHttps || origin.Scheme == Uri.UriSchemeHttp))
        {
            return origin.GetLeftPart(UriPartial.Authority);
        }

        var baseDomain = (configuration["Saas:BaseDomain"] ?? "").Trim().Trim('.');
        if (!string.IsNullOrWhiteSpace(baseDomain))
            return $"{context.Request.Scheme}://{baseDomain}";

        return $"{context.Request.Scheme}://{context.Request.Host}";
    }

    private static string ValidateToken(string? token)
    {
        if (token is null || token.Length != 64 || !token.All(Uri.IsHexDigit))
            throw new ApiException(400, "Liên kết tài khoản không hợp lệ.", "invalid_account_token");
        return AccountTokens.Hash(token.ToLowerInvariant());
    }

    private static string NormalizeIdentifier(string? value)
    {
        var identifier = (value ?? "").Trim().ToLowerInvariant();
        if (identifier.Length is < 3 or > 160)
            throw new ApiException(400, "Nhập email hoặc tên đăng nhập hợp lệ.", "invalid_identifier");
        return identifier;
    }
}

public sealed record AccountTokenRequest(string? Token);
public sealed record AccountEmailRequest(string? Identifier);
public sealed record ResetPasswordWithTokenRequest(string? Token, string? NewPassword);
internal sealed record IssuedAccountToken(string? Slug, string Email, string Token);
