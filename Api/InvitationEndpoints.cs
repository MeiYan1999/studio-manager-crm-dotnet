using System.Net.Mail;
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
        admin.MapGet("", (SaasDatabase database) =>
        {
            using var db = database.Open();
            return Results.Ok(new { GoogleOnly = true,
                Invitations = db.Set<StudioInvitation>().AsEnumerable().Select(i => new
                { i.Id, i.Email, i.Role, i.ExpiresAt, i.AcceptedAt, i.CancelledAt,
                    Status = i.AcceptedAt is not null ? "accepted" : i.CancelledAt is not null ? "cancelled" : i.ExpiresAt <= DateTimeOffset.UtcNow ? "expired" : "pending" }).ToArray() });
        });
        admin.MapPost("", (InviteRequest request, ISubscriptionGuard guard, SaasDatabase database, TenantContext tenant) =>
        {
            guard.Require(StudioFeature.StaffRbac);
            if (string.IsNullOrWhiteSpace(request.Email) || !MailAddress.TryCreate(request.Email, out var email) ||
                !string.Equals(email.Address, request.Email.Trim(), StringComparison.OrdinalIgnoreCase) || request.Email.Length > 160 ||
                request.Role is not ("admin" or "manager" or "sale" or "employee")) throw new ApiException(400, "Email hoặc vai trò không hợp lệ.", "invalid_invitation");
            var invitation = new StudioInvitation { Email = email.Address.ToLowerInvariant(), Role = request.Role,
                PendingKey = email.Address.ToLowerInvariant(), TokenHash = "", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) };
            database.Mutate(tenant.TenantId, (db, studio) =>
            {
                foreach (var expired in db.Set<StudioInvitation>().AsEnumerable().Where(i => i.PendingKey == invitation.PendingKey &&
                    i.AcceptedAt is null && i.CancelledAt is null && i.ExpiresAt <= DateTimeOffset.UtcNow))
                    expired.PendingKey = null;
                var pending = db.Set<StudioInvitation>().AsEnumerable().Count(i => i.AcceptedAt is null && i.CancelledAt is null && i.ExpiresAt > DateTimeOffset.UtcNow);
                if (db.Set<StudioUser>().Count(u => u.IsActive) + pending >= PlanRules.Users(PlanRules.Effective(studio, DateTimeOffset.UtcNow)))
                    throw new ApiException(409, "Đã hết slot user (bao gồm thư mời đang chờ).", "user_slots_full");
                if (db.Set<StudioUser>().Any(u => u.Email == invitation.Email) || db.Set<StudioInvitation>().AsEnumerable().Any(i => i.Email == invitation.Email && i.AcceptedAt is null && i.CancelledAt is null && i.ExpiresAt > DateTimeOffset.UtcNow))
                    throw new ApiException(409, "Email đã có tài khoản hoặc thư mời đang chờ.", "email_in_use");
                db.Add(invitation); return 0;
            });
            return Results.Ok(new { Pending = true, invitation.Email, invitation.ExpiresAt });
        });
        admin.MapPut("/{id}", (string id, UpdateInvitationRequest request, SaasDatabase database, TenantContext tenant) => database.Mutate(tenant.TenantId, (db, studio) =>
        {
            if (request.Role is not ("admin" or "manager" or "sale" or "employee"))
                throw new ApiException(400, "Vai trò không hợp lệ.", "invalid_invitation");
            var invitation = db.Set<StudioInvitation>().SingleOrDefault(i => i.Id == id) ?? throw new ApiException(404, "Không tìm thấy lời mời.", "not_found");
            if (invitation.AcceptedAt is not null) throw new ApiException(409, "Lời mời đã được chấp nhận.", "invitation_accepted");
            var reactivating = invitation.CancelledAt is not null || invitation.ExpiresAt <= DateTimeOffset.UtcNow;
            if (reactivating)
            {
                var activeUsers = db.Set<StudioUser>().Count(user => user.IsActive);
                var activeInvitations = db.Set<StudioInvitation>().AsEnumerable().Count(candidate => candidate.Id != invitation.Id &&
                    candidate.AcceptedAt is null && candidate.CancelledAt is null && candidate.ExpiresAt > DateTimeOffset.UtcNow);
                if (activeUsers + activeInvitations >= PlanRules.Users(PlanRules.Effective(studio, DateTimeOffset.UtcNow)))
                    throw new ApiException(409, "Đã hết slot user (bao gồm lời mời đang chờ).", "user_slots_full");
                if (db.Set<StudioUser>().Any(user => user.Email == invitation.Email) ||
                    db.Set<StudioInvitation>().Any(candidate => candidate.Id != invitation.Id && candidate.PendingKey == invitation.Email))
                    throw new ApiException(409, "Email đã có tài khoản hoặc lời mời đang chờ.", "email_in_use");
                invitation.PendingKey = invitation.Email;
            }
            invitation.Role = request.Role;
            invitation.CancelledAt = null;
            if (request.Renew || invitation.ExpiresAt <= DateTimeOffset.UtcNow) invitation.ExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
            return Results.Ok(new { Updated = true, invitation.ExpiresAt });
        }));
        admin.MapDelete("/{id}", (string id, SaasDatabase database, TenantContext tenant) => database.Mutate(tenant.TenantId, (db, _) =>
        {
            var invitation = db.Set<StudioInvitation>().SingleOrDefault(i => i.Id == id) ?? throw new ApiException(404, "Không tìm thấy thư mời.", "not_found");
            invitation.CancelledAt = DateTimeOffset.UtcNow;
            invitation.PendingKey = null;
            return Results.NoContent();
        }));
        app.MapPost("/api/saas/invitations/accept", (AcceptInvitationRequest request, TenantContext tenant, SaasDatabase database, HttpContext context, AdminSessionStore sessions) =>
        {
            if (request.Token is null || request.Token.Length != 64 || request.Password is null || request.Password.Length is < 8 or > 128 ||
                request.Username is null || !System.Text.RegularExpressions.Regex.IsMatch(request.Username, "^[a-z0-9._-]{3,32}$"))
                throw new ApiException(400, "Thông tin tạo tài khoản không hợp lệ (mật khẩu tối thiểu 8 ký tự).", "invalid_invitation");
            var hashed = AccountTokens.Hash(request.Token);
            var user = database.Mutate(tenant.TenantId, (db, studio) =>
            {
                if (PlanRules.Effective(studio, DateTimeOffset.UtcNow) != SubscriptionPlan.ErpStudio)
                    throw new ApiException(403, "Studio cần gói ERP STUDIO để nhận thành viên mới.", "upgrade_required");
                var invitation = db.Set<StudioInvitation>().SingleOrDefault(i => i.TokenHash != "" && i.TokenHash == hashed);
                if (invitation is null || invitation.AcceptedAt is not null || invitation.ExpiresAt <= DateTimeOffset.UtcNow)
                    throw new ApiException(400, "Thư mời đã hết hạn hoặc đã sử dụng.", "invalid_invitation");
                if (db.Set<StudioUser>().Count(u => u.IsActive) >= 5) throw new ApiException(409, "Đã hết slot user.", "user_slots_full");
                if (db.Set<StudioUser>().Any(u => u.Username == request.Username)) throw new ApiException(409, "Tên đăng nhập đã có người dùng.", "username_taken");
                var user = new StudioUser { Username = request.Username, Email = invitation.Email, DisplayName = AlbumText.TrimTo(request.DisplayName, 60),
                    Role = invitation.Role, PasswordHash = PasswordHasher.Hash(request.Password), PermissionsConfigured = true,
                    Permissions = invitation.Role is "admin" or "manager" ? StudioPermissions.All.ToList() : invitation.Role == "sale"
                        ? [StudioPermissions.Dashboard, StudioPermissions.Albums, StudioPermissions.Tools, StudioPermissions.Customers, StudioPermissions.Bookings, StudioPermissions.Orders, StudioPermissions.Contracts, StudioPermissions.Packages]
                        : StudioPermissions.EmployeeDefaults.ToList() };
                db.Add(user); invitation.AcceptedAt = DateTimeOffset.UtcNow; invitation.PendingKey = null; return user;
            });
            sessions.SignIn(context, user);
            return Results.Ok(new { Accepted = true });
        });
    }
}
public sealed record InviteRequest(string Email, string Role);
public sealed record UpdateInvitationRequest(string Role, bool Renew);
public sealed record AcceptInvitationRequest(string Token, string Username, string Password, string DisplayName);
