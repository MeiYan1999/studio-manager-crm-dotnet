using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StudioCore.Models;
using StudioCore.Persistence;
using StudioCore.Services;

namespace StudioCore.Api;

public static class InvitationEndpoints
{
    public static void Map(WebApplication app)
    {
        var admin = app.MapGroup("/api/saas/invitations").AddEndpointFilter<StudioAdminFilter>();
        admin.MapGet("", (SaasDatabase database, IConfiguration config) =>
        {
            using var db = database.Open();
            return Results.Ok(new { EmailConfigured = !string.IsNullOrWhiteSpace(config["Email:SmtpHost"]) && !string.IsNullOrWhiteSpace(config["Email:From"]),
                Invitations = db.Set<StudioInvitation>().AsEnumerable().Select(i => new { i.Id, i.Email, i.Role, i.ExpiresAt, i.AcceptedAt }).ToArray() });
        });
        admin.MapPost("", async (InviteRequest request, ISubscriptionGuard guard, SaasDatabase database, TenantContext tenant, IConfiguration config) =>
        {
            guard.Require(StudioFeature.StaffRbac);
            if (string.IsNullOrWhiteSpace(config["Email:SmtpHost"]) || string.IsNullOrWhiteSpace(config["Email:From"]) || string.IsNullOrWhiteSpace(config["Saas:PublicOrigin"]))
                throw new ApiException(503, "Chưa cấu hình SMTP và địa chỉ nền tảng để gửi thư mời.", "email_not_configured");
            if (string.IsNullOrWhiteSpace(request.Email) || !MailAddress.TryCreate(request.Email, out var email) || email.Address != request.Email.Trim() || request.Email.Length > 160 ||
                request.Role is not ("admin" or "manager" or "sale" or "employee")) throw new ApiException(400, "Email hoặc vai trò không hợp lệ.", "invalid_invitation");
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var invitation = new StudioInvitation { Email = email.Address.ToLowerInvariant(), Role = request.Role,
                TokenHash = Hash(token), ExpiresAt = DateTimeOffset.UtcNow.AddDays(3) };
            var slug = database.Mutate(tenant.TenantId, (db, studio) =>
            {
                if (studio.Slug is null) throw new ApiException(400, "Lưu tên định danh studio trước khi mời thành viên.", "slug_required");
                var pending = db.Set<StudioInvitation>().AsEnumerable().Count(i => i.AcceptedAt is null && i.ExpiresAt > DateTimeOffset.UtcNow);
                if (db.Set<StudioUser>().Count(u => u.IsActive) + pending >= PlanRules.Users(PlanRules.Effective(studio, DateTimeOffset.UtcNow)))
                    throw new ApiException(409, "Đã hết slot user (bao gồm thư mời đang chờ).", "user_slots_full");
                if (db.Set<StudioUser>().Any(u => u.Email == invitation.Email) || db.Set<StudioInvitation>().AsEnumerable().Any(i => i.Email == invitation.Email && i.AcceptedAt is null && i.ExpiresAt > DateTimeOffset.UtcNow))
                    throw new ApiException(409, "Email đã có tài khoản hoặc thư mời đang chờ.", "email_in_use");
                db.Add(invitation); return studio.Slug;
            });
            try
            {
                using var smtp = new SmtpClient(config["Email:SmtpHost"], config.GetValue("Email:SmtpPort", 587)) { EnableSsl = config.GetValue("Email:EnableSsl", true) };
                if (!string.IsNullOrWhiteSpace(config["Email:Username"])) smtp.Credentials = new NetworkCredential(config["Email:Username"], config["Email:Password"]);
                var url = config["Saas:PublicOrigin"]!.TrimEnd('/') + "/s/" + slug + "/admin/accept-invite?token=" + token;
                using var mail = new MailMessage(config["Email:From"]!, invitation.Email, "Lời mời tham gia studio · StudioCore", "Bạn được mời tham gia studio với vai trò " + invitation.Role + ".\nMở liên kết để tạo mật khẩu (hiệu lực 3 ngày):\n" + url);
                await smtp.SendMailAsync(mail);
            }
            catch (Exception e) when (e is SmtpException or InvalidOperationException)
            {
                database.Mutate(tenant.TenantId, (db, _) => { db.Set<StudioInvitation>().Single(i => i.Id == invitation.Id).ExpiresAt = DateTimeOffset.UtcNow; return 0; });
                throw new ApiException(503, "Chưa gửi được email. Thư mời đã được thu hồi; hãy kiểm tra cấu hình SMTP.", "email_failed");
            }
            return Results.Ok(new { Sent = true });
        });
        admin.MapDelete("/{id}", (string id, SaasDatabase database, TenantContext tenant) => database.Mutate(tenant.TenantId, (db, _) =>
        {
            var invitation = db.Set<StudioInvitation>().SingleOrDefault(i => i.Id == id) ?? throw new ApiException(404, "Không tìm thấy thư mời.", "not_found");
            invitation.ExpiresAt = DateTimeOffset.UtcNow; return Results.NoContent();
        }));
        app.MapPost("/api/saas/invitations/accept", (AcceptInvitationRequest request, TenantContext tenant, SaasDatabase database, HttpContext context, AdminSessionStore sessions) =>
        {
            if (request.Token is null || request.Token.Length != 64 || request.Password is null || request.Password.Length is < 8 or > 128 ||
                request.Username is null || !System.Text.RegularExpressions.Regex.IsMatch(request.Username, "^[a-z0-9._-]{3,32}$"))
                throw new ApiException(400, "Thông tin tạo tài khoản không hợp lệ (mật khẩu tối thiểu 8 ký tự).", "invalid_invitation");
            var hashed = Hash(request.Token);
            var user = database.Mutate(tenant.TenantId, (db, studio) =>
            {
                if (PlanRules.Effective(studio, DateTimeOffset.UtcNow) != SubscriptionPlan.ErpStudio)
                    throw new ApiException(403, "Studio cần gói ERP STUDIO để nhận thành viên mới.", "upgrade_required");
                var invitation = db.Set<StudioInvitation>().SingleOrDefault(i => i.TokenHash == hashed);
                if (invitation is null || invitation.AcceptedAt is not null || invitation.ExpiresAt <= DateTimeOffset.UtcNow)
                    throw new ApiException(400, "Thư mời đã hết hạn hoặc đã sử dụng.", "invalid_invitation");
                if (db.Set<StudioUser>().Count(u => u.IsActive) >= 5) throw new ApiException(409, "Đã hết slot user.", "user_slots_full");
                if (db.Set<StudioUser>().Any(u => u.Username == request.Username)) throw new ApiException(409, "Tên đăng nhập đã có người dùng.", "username_taken");
                var user = new StudioUser { Username = request.Username, Email = invitation.Email, DisplayName = AlbumText.TrimTo(request.DisplayName, 60),
                    Role = invitation.Role, PasswordHash = PasswordHasher.Hash(request.Password), PermissionsConfigured = true,
                    Permissions = invitation.Role is "admin" or "manager" ? StudioPermissions.All.ToList() : invitation.Role == "sale"
                        ? [StudioPermissions.Dashboard, StudioPermissions.Albums, StudioPermissions.Tools, StudioPermissions.Customers, StudioPermissions.Bookings, StudioPermissions.Orders, StudioPermissions.Contracts, StudioPermissions.Packages]
                        : StudioPermissions.EmployeeDefaults.ToList() };
                db.Add(user); invitation.AcceptedAt = DateTimeOffset.UtcNow; return user;
            });
            sessions.SignIn(context, user);
            return Results.Ok(new { Accepted = true });
        });
    }
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
public sealed record InviteRequest(string Email, string Role);
public sealed record AcceptInvitationRequest(string Token, string Username, string Password, string DisplayName);
