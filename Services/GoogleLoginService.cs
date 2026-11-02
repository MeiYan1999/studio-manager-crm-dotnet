using System.Data;
using Microsoft.EntityFrameworkCore;
using StudioCore.Models;
using StudioCore.Persistence;

namespace StudioCore.Services;

public sealed class GoogleLoginService(SaasDatabase database, StudioProvisioning provisioning)
{
    private readonly object gate = new();

    public GoogleLoginResult SignInOrProvision(string subject, string email, string displayName)
    {
        subject = subject.Trim();
        email = email.Trim().ToLowerInvariant();
        displayName = displayName.Trim();
        if (subject.Length == 0 || email.Length == 0)
            throw new ApiException(400, "Google không trả về đủ thông tin tài khoản.", "google_profile_incomplete");

        lock (gate)
        {
            UpsertIdentity(subject, email, displayName);
            LinkSingleLegacyMembership(subject, email);
            var hadPendingInvitation = AcceptPendingInvitations(subject, email, displayName);

            var workspaces = LoadWorkspaces(subject);
            var created = false;
            if (workspaces.Count == 0)
            {
                if (hadPendingInvitation)
                    throw new ApiException(409, "Lời mời đang chờ nhưng studio chưa còn slot hoặc chưa đủ gói. Vui lòng liên hệ quản trị viên studio.", "invitation_pending_unavailable");
                provisioning.CreateFromGoogle(subject, email, displayName);
                workspaces = LoadWorkspaces(subject);
                created = true;
            }
            return new GoogleLoginResult(subject, workspaces, created);
        }
    }

    public GoogleWorkspace SelectWorkspace(string subject, string tenantId)
    {
        using (var lookup = database.Open(Studio.MasterId))
        {
            var identity = lookup.GoogleIdentities.AsNoTracking().SingleOrDefault(candidate => candidate.GoogleSubject == subject);
            if (identity is null || !identity.IsActive)
                throw new ApiException(403, "Tài khoản Google đã bị khóa.", "account_disabled");
        }

        return database.Mutate(tenantId, (db, studio) =>
        {
            var user = db.Set<StudioUser>().SingleOrDefault(candidate => candidate.GoogleSubject == subject);
            if (user is null || !user.IsActive)
                throw new ApiException(403, "Bạn không có quyền truy cập studio này.", "workspace_forbidden");
            user.LastLoginAt = DateTimeOffset.UtcNow;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            return new GoogleWorkspace(studio, user);
        });
    }

    public IReadOnlyList<GoogleWorkspaceSummary> ListWorkspaces(string subject)
    {
        using (var lookup = database.Open(Studio.MasterId))
            if (!lookup.GoogleIdentities.Any(identity => identity.GoogleSubject == subject && identity.IsActive))
                throw new ApiException(403, "Tài khoản Google đã bị khóa.", "account_disabled");
        return LoadWorkspaces(subject).Select(workspace => new GoogleWorkspaceSummary(
            workspace.Studio.Id, workspace.Studio.Name, workspace.Studio.Slug ?? "", workspace.User.Role)).ToArray();
    }

    private void UpsertIdentity(string subject, string email, string displayName)
    {
        using var db = database.Open(Studio.MasterId);
        using var tx = db.Database.BeginTransaction(IsolationLevel.Serializable);
        var identity = db.GoogleIdentities.SingleOrDefault(candidate => candidate.GoogleSubject == subject);
        if (identity is null)
        {
            db.GoogleIdentities.Add(new GoogleIdentity
            {
                GoogleSubject = subject,
                Email = email,
                DisplayName = displayName,
                LastLoginAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            if (!identity.IsActive)
                throw new ApiException(403, "Tài khoản Google đã bị khóa.", "account_disabled");
            identity.Email = email;
            identity.DisplayName = displayName;
            identity.LastLoginAt = DateTimeOffset.UtcNow;
        }
        db.SaveChanges();
        tx.Commit();
    }

    private void LinkSingleLegacyMembership(string subject, string email)
    {
        using var lookup = database.Open(Studio.MasterId);
        if (lookup.Set<StudioUser>().IgnoreQueryFilters().Any(user => user.GoogleSubject == subject)) return;
        var matches = lookup.Set<StudioUser>().IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.GoogleSubject == null && user.IsEmailVerified && user.Email.ToLower() == email).ToList();
        if (matches.Count != 1) return;
        var match = matches[0];
        database.Mutate(match.TenantId, (db, _) =>
        {
            var user = db.Set<StudioUser>().Single(candidate => candidate.Id == match.Id);
            user.GoogleSubject = subject;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            return 0;
        });
    }

    private bool AcceptPendingInvitations(string subject, string email, string displayName)
    {
        string[] tenantIds;
        using (var lookup = database.Open(Studio.MasterId))
        {
            var now = DateTimeOffset.UtcNow;
            tenantIds = lookup.Set<StudioInvitation>().IgnoreQueryFilters().AsNoTracking()
                .Where(invitation => invitation.Email == email && invitation.AcceptedAt == null && invitation.CancelledAt == null)
                .AsEnumerable().Where(invitation => invitation.ExpiresAt > now)
                .Select(invitation => invitation.TenantId).Distinct().ToArray();
        }

        foreach (var tenantId in tenantIds)
        {
            database.Mutate(tenantId, (db, studio) =>
            {
                var invitation = db.Set<StudioInvitation>().Where(candidate => candidate.Email == email &&
                    candidate.AcceptedAt == null && candidate.CancelledAt == null).AsEnumerable()
                    .Where(candidate => candidate.ExpiresAt > DateTimeOffset.UtcNow)
                    .OrderByDescending(candidate => candidate.ExpiresAt).FirstOrDefault();
                if (invitation is null) return 0;
                if (PlanRules.Effective(studio, DateTimeOffset.UtcNow) != SubscriptionPlan.ErpStudio)
                    return 0; // Keep it pending until the studio restores the required plan.

                var existing = db.Set<StudioUser>().SingleOrDefault(user => user.GoogleSubject == subject);
                if (existing is null)
                {
                    var active = db.Set<StudioUser>().Count(user => user.IsActive);
                    if (active >= PlanRules.Users(PlanRules.Effective(studio, DateTimeOffset.UtcNow)))
                        return 0; // Keep it pending; the admin can free a slot and ask the user to sign in again.
                    var username = UniqueUsername(db, email.Split('@')[0]);
                    db.Add(new StudioUser
                    {
                        GoogleSubject = subject,
                        Username = username,
                        Email = email,
                        DisplayName = AlbumText.TrimTo(displayName, 60),
                        Role = invitation.Role,
                        PasswordHash = PasswordHasher.Hash(Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))),
                        IsEmailVerified = true,
                        PermissionsConfigured = true,
                        Permissions = PermissionsFor(invitation.Role)
                    });
                }
                invitation.AcceptedAt = DateTimeOffset.UtcNow;
                invitation.PendingKey = null;
                return 0;
            });
        }
        return tenantIds.Length > 0;
    }

    private List<GoogleWorkspace> LoadWorkspaces(string subject)
    {
        using var lookup = database.Open(Studio.MasterId);
        var memberships = lookup.Set<StudioUser>().IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.GoogleSubject == subject && user.IsActive).ToList();
        var studios = lookup.Studios.AsNoTracking().Where(studio => memberships.Select(user => user.TenantId).Contains(studio.Id))
            .ToDictionary(studio => studio.Id);
        return memberships.Where(user => studios.ContainsKey(user.TenantId))
            .Select(user => new GoogleWorkspace(studios[user.TenantId], user))
            .OrderBy(workspace => workspace.Studio.Name).ToList();
    }

    private static string UniqueUsername(StudioDbContext db, string value)
    {
        var root = new string(value.ToLowerInvariant().Where(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-').ToArray());
        if (root.Length < 3) root = "google-user";
        if (root.Length > 24) root = root[..24];
        var candidate = root;
        for (var suffix = 2; db.Set<StudioUser>().Any(user => user.Username == candidate); suffix++) candidate = $"{root}-{suffix}";
        return candidate;
    }

    private static List<string> PermissionsFor(string role) => role is "admin" or "manager"
        ? StudioPermissions.All.ToList()
        : role == "sale"
            ? [StudioPermissions.Dashboard, StudioPermissions.Albums, StudioPermissions.Tools, StudioPermissions.Customers,
                StudioPermissions.Bookings, StudioPermissions.Orders, StudioPermissions.Contracts, StudioPermissions.Packages]
            : StudioPermissions.EmployeeDefaults.ToList();
}

public sealed record GoogleWorkspace(Studio Studio, StudioUser User);
public sealed record GoogleWorkspaceSummary(string TenantId, string Name, string Slug, string Role);
public sealed record GoogleLoginResult(string GoogleSubject, IReadOnlyList<GoogleWorkspace> Workspaces, bool IsNewStudio);
