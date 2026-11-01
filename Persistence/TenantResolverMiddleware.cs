using Microsoft.EntityFrameworkCore;
using StudioCore.Models;

namespace StudioCore.Persistence;

public sealed class TenantResolverMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SaasDatabase database, IConfiguration configuration)
    {
        var path = context.Request.Path.Value ?? "/";
        // Provider callbacks are server-to-server and must never use a visitor's tenant context.
        if (path == "/api/payments/sepay-webhook" || path == "/api/health" || path.StartsWith("/assets/", StringComparison.Ordinal))
        { await next(context); return; }
        using var db = database.Open(Studio.MasterId);
        var baseDomain = (configuration["Saas:BaseDomain"] ?? configuration["RENDER_EXTERNAL_HOSTNAME"] ?? "localhost").Trim().ToLowerInvariant();
        var host = context.Request.Host.Host.ToLowerInvariant().TrimEnd('.');
        Studio? studio;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0 && segments[0] == "s")
        {
            if (segments.Length < 2) throw new ApiException(404, "Thiếu tên định danh studio.", "tenant_not_found");
            var slug = segments[1].ToLowerInvariant();
            studio = await db.Studios.SingleOrDefaultAsync(s => s.Slug == slug, context.RequestAborted);
            // A tenant path is only accepted on the platform host, never on another studio's domain.
            if (host != baseDomain && host != "www." + baseDomain && host != "localhost" && host != "127.0.0.1") studio = null;
            if (studio is not null)
            {
                var prefix = "/s/" + segments[1];
                context.Request.PathBase = prefix;
                context.Request.Path = path.Length == prefix.Length ? "/" : path[prefix.Length..];
            }
        }
        else if (host == baseDomain || host == "www." + baseDomain || host == "localhost" || host == "127.0.0.1")
        {
            context.Items["StudioCore.PlatformHost"] = true;
            studio = await db.Studios.SingleOrDefaultAsync(s => s.Id == Studio.MasterId, context.RequestAborted);
        }
        else if (host.EndsWith("." + baseDomain, StringComparison.Ordinal))
        {
            var slug = host[..^(baseDomain.Length + 1)];
            studio = await db.Studios.SingleOrDefaultAsync(s => s.Slug == slug, context.RequestAborted);
        }
        else
        {
            studio = await db.Studios.SingleOrDefaultAsync(s => s.CustomDomain == host && s.CustomDomainVerified, context.RequestAborted);
            if (studio is not null && (studio.CurrentPlan < SubscriptionPlan.AlbumPro ||
                (studio.SubscriptionExpiresAt is { } until && until <= DateTimeOffset.UtcNow))) studio = null;
        }
        if (studio is null) throw new ApiException(404, "Không tìm thấy studio hoặc tên miền chưa được xác minh.", "tenant_not_found");
        TenantContext.Set(context, studio.Id);
        context.Response.Headers["X-Studio-Code"] = studio.StudioCode;
        await next(context);
    }
}
