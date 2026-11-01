using StudioCore.Models;

namespace StudioCore.Persistence;

// Singleton accessor; values are stored on the request, never in singleton mutable fields.
public sealed class TenantContext(IHttpContextAccessor accessor)
{
    internal const string ItemKey = "StudioCore.Tenant";
    public string TenantId => (accessor.HttpContext?.Items[ItemKey] as string) ?? Studio.MasterId;
    public static void Set(HttpContext context, string tenantId) => context.Items[ItemKey] = tenantId;
}
