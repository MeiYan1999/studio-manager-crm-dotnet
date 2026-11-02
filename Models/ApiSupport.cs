using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StudioCore.Services;
using StudioCore.Persistence;

namespace StudioCore.Models;

public sealed class ApiException : Exception
{
    public ApiException(int statusCode, string message, string? code = null, string? feature = null, string? requiredPlan = null) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Feature = feature;
        RequiredPlan = requiredPlan;
    }

    public int StatusCode { get; }
    public string? Code { get; }
    public string? Feature { get; }
    public string? RequiredPlan { get; }
}

public sealed class AdminSessionStore
{
    public const string CookieName = "may_studio_session";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(14);
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _revocationVersions = new(StringComparer.Ordinal);
    private readonly DataStore _dataStore;
    private readonly SaasDatabase _saasDatabase;
    private readonly byte[] _codeHash;

    public AdminSessionStore(DataStore dataStore, SaasDatabase saasDatabase)
    {
        _dataStore = dataStore;
        _saasDatabase = saasDatabase;
        _codeHash = Hash(dataStore.AdminCode);
    }

    /// <summary>Legacy access-code check, kept for API clients using the old login.</summary>
    public bool VerifyCode(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Hash(code), _codeHash);
    }

    private static string Cookie(HttpContext context)
    {
        var tenant = context.RequestServices.GetRequiredService<TenantContext>().TenantId;
        return tenant == Studio.MasterId ? CookieName : CookieName + "_" + tenant;
    }

    public StudioUser? GetCurrentUser(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(Cookie(context), out var token) ||
            string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        if (!_sessions.TryGetValue(token, out var entry))
        {
            return null;
        }

        if (entry.TenantId != context.RequestServices.GetRequiredService<TenantContext>().TenantId) return null;

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return null;
        }

        var user = FindUser(entry.TenantId, entry.UserId);
        if (user is null ||
            !user.IsActive ||
            !IsIdentityActive(user.GoogleSubject) ||
            entry.RevocationVersion != GetRevocationVersion(entry.TenantId, entry.UserId) ||
            !CryptographicOperations.FixedTimeEquals(entry.PasswordFingerprint, Fingerprint(user.PasswordHash)))
        {
            _sessions.TryRemove(token, out _);
            return null;
        }

        return user;
    }

    private bool IsIdentityActive(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return true;
        using var db = _saasDatabase.Open(Studio.MasterId);
        return db.GoogleIdentities.Any(identity => identity.GoogleSubject == subject && identity.IsActive);
    }

    private StudioUser? FindUser(string tenantId, string userId)
    {
        using var db = _saasDatabase.Open(tenantId);
        return db.Set<StudioUser>().AsNoTracking().SingleOrDefault(user => user.Id == userId);
    }

    public bool IsAuthenticated(HttpContext context) => GetCurrentUser(context) is not null;

    public void RevokeUserSessions(HttpContext context, string userId)
    {
        var tenantId = context.RequestServices.GetRequiredService<TenantContext>().TenantId;
        _revocationVersions.AddOrUpdate(RevocationKey(tenantId, userId), 1, (_, version) => version + 1);
        foreach (var entry in _sessions)
            if (entry.Value.TenantId == tenantId && entry.Value.UserId == userId)
                _sessions.TryRemove(entry.Key, out _);
    }

    public void RevokeIdentitySessions(string googleSubject)
    {
        using var db = _saasDatabase.Open(Studio.MasterId);
        var memberships = db.Set<StudioUser>().IgnoreQueryFilters()
            .Where(user => user.GoogleSubject == googleSubject)
            .Select(user => new { user.TenantId, user.Id }).ToArray();
        foreach (var membership in memberships)
        {
            _revocationVersions.AddOrUpdate(RevocationKey(membership.TenantId, membership.Id), 1, (_, version) => version + 1);
            foreach (var entry in _sessions)
                if (entry.Value.TenantId == membership.TenantId && entry.Value.UserId == membership.Id)
                    _sessions.TryRemove(entry.Key, out _);
        }
    }

    public StudioUser RequireUser(HttpContext context) =>
        GetCurrentUser(context)
        ?? throw new ApiException(401, "Phiên đăng nhập đã hết hạn.", "unauthorized");

    public void SignIn(HttpContext context, StudioUser user)
        => SignIn(context, user, expectedRevocationVersion: null);

    public long CaptureRevocationVersion(HttpContext context, string userId)
    {
        var tenantId = context.RequestServices.GetRequiredService<TenantContext>().TenantId;
        return GetRevocationVersion(tenantId, userId);
    }

    public void SignIn(HttpContext context, StudioUser user, long? expectedRevocationVersion)
    {
        PruneExpired();
        var tenantId = context.RequestServices.GetRequiredService<TenantContext>().TenantId;
        var current = FindUser(tenantId, user.Id);
        var version = GetRevocationVersion(tenantId, user.Id);
        if (current is null ||
            !current.IsActive ||
            (expectedRevocationVersion is not null && expectedRevocationVersion.Value != version) ||
            !CryptographicOperations.FixedTimeEquals(Fingerprint(current.PasswordHash), Fingerprint(user.PasswordHash)))
        {
            throw new ApiException(401, "Tài khoản hoặc thông tin đăng nhập đã thay đổi. Vui lòng đăng nhập lại.", "session_revoked");
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _sessions[token] = new SessionEntry(
            current.Id,
            tenantId,
            DateTimeOffset.UtcNow.Add(SessionLifetime),
            version,
            Fingerprint(current.PasswordHash));
        context.Response.Cookies.Append(Cookie(context), token, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = SessionLifetime,
            IsEssential = true
        });
    }

    public void SignOut(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(Cookie(context), out var token))
        {
            _sessions.TryRemove(token, out _);
        }

        context.Response.Cookies.Delete(Cookie(context), new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax
        });
    }

    private void PruneExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _sessions)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                _sessions.TryRemove(pair.Key, out _);
            }
        }
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private static byte[] Fingerprint(string passwordHash) => Hash(passwordHash ?? "");
    private static string RevocationKey(string tenantId, string userId) => tenantId + "\n" + userId;
    private long GetRevocationVersion(string tenantId, string userId) =>
        _revocationVersions.GetValueOrDefault(RevocationKey(tenantId, userId));

    private sealed record SessionEntry(
        string UserId,
        string TenantId,
        DateTimeOffset ExpiresAt,
        long RevocationVersion,
        byte[] PasswordFingerprint);
}

public static class StudioAccess
{
    public static bool IsAdmin(StudioUser? user) =>
        string.Equals(user?.Role, "admin", StringComparison.Ordinal);

    public static bool Has(StudioUser? user, string permission) =>
        user is not null &&
        (IsAdmin(user) ||
         (user.Permissions ?? []).Contains(permission, StringComparer.Ordinal));
}

public sealed class AdminOnlyFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var sessions = context.HttpContext.RequestServices.GetRequiredService<AdminSessionStore>();
        var user = sessions.GetCurrentUser(context.HttpContext);
        if (user is null)
        {
            return Results.Json(
                new ApiErrorDto("Phiên đăng nhập đã hết hạn.", "unauthorized"),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (user.MustChangePassword)
        {
            return Results.Json(
                new ApiErrorDto("Bạn cần đổi mật khẩu trước khi tiếp tục.", "password_change_required"),
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}

public sealed class StudioAdminFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var sessions = context.HttpContext.RequestServices.GetRequiredService<AdminSessionStore>();
        var user = sessions.GetCurrentUser(context.HttpContext);
        if (user is null)
        {
            return Results.Json(
                new ApiErrorDto("Phiên đăng nhập đã hết hạn.", "unauthorized"),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (user.MustChangePassword)
        {
            return Results.Json(
                new ApiErrorDto("Bạn cần đổi mật khẩu trước khi tiếp tục.", "password_change_required"),
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!StudioAccess.IsAdmin(user))
        {
            return Results.Json(
                new ApiErrorDto("Chỉ quản trị viên mới có thể thực hiện thao tác này.", "forbidden"),
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}

public sealed class StudioPermissionFilter : IEndpointFilter
{
    private readonly string _permission;

    public StudioPermissionFilter(string permission)
    {
        _permission = permission;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var sessions = context.HttpContext.RequestServices.GetRequiredService<AdminSessionStore>();
        var user = sessions.GetCurrentUser(context.HttpContext);
        if (user is null)
        {
            return Results.Json(
                new ApiErrorDto("Phiên đăng nhập đã hết hạn.", "unauthorized"),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (user.MustChangePassword)
        {
            return Results.Json(
                new ApiErrorDto("Bạn cần đổi mật khẩu trước khi tiếp tục.", "password_change_required"),
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!StudioAccess.Has(user, _permission))
        {
            return Results.Json(
                new ApiErrorDto("Bạn chưa được cấp quyền cho phân hệ này.", "forbidden"),
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}

/// <summary>Allows the request when the user has at least one of the listed permissions.</summary>
public sealed class StudioAnyPermissionFilter : IEndpointFilter
{
    private readonly string[] _permissions;

    public StudioAnyPermissionFilter(params string[] permissions)
    {
        _permissions = permissions;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var sessions = context.HttpContext.RequestServices.GetRequiredService<AdminSessionStore>();
        var user = sessions.GetCurrentUser(context.HttpContext);
        if (user is null)
        {
            return Results.Json(
                new ApiErrorDto("Phiên đăng nhập đã hết hạn.", "unauthorized"),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (user.MustChangePassword)
        {
            return Results.Json(
                new ApiErrorDto("Bạn cần đổi mật khẩu trước khi tiếp tục.", "password_change_required"),
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!_permissions.Any(permission => StudioAccess.Has(user, permission)))
        {
            return Results.Json(
                new ApiErrorDto("Bạn chưa được cấp quyền cho phân hệ này.", "forbidden"),
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}
