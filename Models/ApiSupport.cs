using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using MayAlbum.Services;

namespace MayAlbum.Models;

public sealed class ApiException : Exception
{
    public ApiException(int statusCode, string message, string? code = null) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }
    public string? Code { get; }
}

public sealed class AdminSessionStore
{
    public const string CookieName = "may_studio_session";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(14);
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new(StringComparer.Ordinal);
    private readonly DataStore _dataStore;
    private readonly byte[] _codeHash;

    public AdminSessionStore(DataStore dataStore)
    {
        _dataStore = dataStore;
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

    public StudioUser? GetCurrentUser(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var token) ||
            string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        if (!_sessions.TryGetValue(token, out var entry))
        {
            return null;
        }

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return null;
        }

        var user = _dataStore.Read(data => data.Users.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, entry.UserId, StringComparison.Ordinal)));
        if (user is null || !user.IsActive)
        {
            _sessions.TryRemove(token, out _);
            return null;
        }

        return user;
    }

    public bool IsAuthenticated(HttpContext context) => GetCurrentUser(context) is not null;

    public StudioUser RequireUser(HttpContext context) =>
        GetCurrentUser(context)
        ?? throw new ApiException(401, "Phiên đăng nhập đã hết hạn.", "unauthorized");

    public void SignIn(HttpContext context, StudioUser user)
    {
        PruneExpired();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _sessions[token] = new SessionEntry(user.Id, DateTimeOffset.UtcNow.Add(SessionLifetime));
        context.Response.Cookies.Append(CookieName, token, new CookieOptions
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
        if (context.Request.Cookies.TryGetValue(CookieName, out var token))
        {
            _sessions.TryRemove(token, out _);
        }

        context.Response.Cookies.Delete(CookieName, new CookieOptions
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

    private sealed record SessionEntry(string UserId, DateTimeOffset ExpiresAt);
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
