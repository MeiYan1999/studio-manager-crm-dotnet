using StudioCore.Models;
using StudioCore.Persistence;
using Microsoft.EntityFrameworkCore;

namespace StudioCore.Services;

public enum StudioFeature { Albums, AiFaceSearch, ClientSelections, PcFilter, CustomDomain, Backup, SignatureWatermark, VideoReview, Crm, CalendarSync, Cashbook, FreelancerWages, SheetsExport, StaffRbac, MonthlyPayroll, CrewAssignment, CashHandover }

public static class PlanRules
{
    public static SubscriptionPlan Effective(Studio studio, DateTimeOffset now) =>
        studio.SubscriptionExpiresAt is { } end && end <= now ? SubscriptionPlan.Free : studio.CurrentPlan;
    public static int Users(SubscriptionPlan plan) => plan == SubscriptionPlan.ErpStudio ? 5 : 1;
    public static int FilterQuota(SubscriptionPlan plan) => plan == SubscriptionPlan.Free ? 5 : -1;
    public static decimal Price(SubscriptionPlan plan, BillingCycle cycle) => (plan switch
    { SubscriptionPlan.AlbumPro => 75000m, SubscriptionPlan.ErpSolo => 125000m, SubscriptionPlan.ErpStudio => 225000m, _ => 0m }) * (cycle == BillingCycle.Yearly ? 12 : 1);
    public static SubscriptionPlan Required(StudioFeature feature) => feature switch
    {
        StudioFeature.CustomDomain or StudioFeature.Backup or StudioFeature.SignatureWatermark or StudioFeature.VideoReview => SubscriptionPlan.AlbumPro,
        StudioFeature.Crm or StudioFeature.CalendarSync or StudioFeature.Cashbook or StudioFeature.FreelancerWages or StudioFeature.SheetsExport => SubscriptionPlan.ErpSolo,
        StudioFeature.StaffRbac or StudioFeature.MonthlyPayroll or StudioFeature.CrewAssignment or StudioFeature.CashHandover => SubscriptionPlan.ErpStudio,
        _ => SubscriptionPlan.Free
    };
    public static string Month(DateTimeOffset now) => now.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
    public static void Refresh(Studio studio, DateTimeOffset now)
    {
        var effective = Effective(studio, now);
        studio.MaxUsers = Users(effective);
        studio.MonthlyPcFilterQuota = FilterQuota(effective);
        var month = Month(now);
        if (studio.PcFilterMonth != month) { studio.PcFilterMonth = month; studio.CurrentMonthPcFilterUsed = 0; }
    }
}

public interface ISubscriptionGuard
{
    Studio Current();
    void Require(StudioFeature feature);
}

public sealed class SubscriptionGuard(SaasDatabase database) : ISubscriptionGuard
{
    public Studio Current() { using var db = database.Open(); return db.Studios.Single(s => s.Id == db.TenantId); }
    public void Require(StudioFeature feature)
    {
        var studio = Current();
        var required = PlanRules.Required(feature);
        if (PlanRules.Effective(studio, DateTimeOffset.UtcNow) < required)
            throw new ApiException(403, $"Tính năng này cần gói {required}. Vui lòng nâng cấp trong Cài Đặt.", "subscription_feature_locked", feature.ToString(), required.ToString());
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresSubscriptionAttribute(StudioFeature feature) : Attribute { public StudioFeature Feature { get; } = feature; }

// Attached to the entire existing admin API group so direct API calls cannot bypass the UI.
public sealed class SubscriptionGuardFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var path = http.Request.Path.Value ?? "";
        var tail = path.StartsWith("/api/admin/", StringComparison.Ordinal) ? path[11..] : "";
        var area = tail.Split('/')[0];
        StudioFeature? feature = area switch
        {
            "staff" when !HttpMethods.IsGet(http.Request.Method) => StudioFeature.StaffRbac,
            "customers" or "bookings" or "orders" or "payments" or "contracts" or "reports" or "crm" or "staff" => StudioFeature.Crm,
            "finance" or "expenses" => StudioFeature.Cashbook,
            "payrolls" => StudioFeature.MonthlyPayroll,
            "shifts" or "attendance" => StudioFeature.CrewAssignment,
            "cash-handover" => StudioFeature.CashHandover,
            "users" when !HttpMethods.IsGet(http.Request.Method) => StudioFeature.StaffRbac,
            _ => null
        };
        feature = http.GetEndpoint()?.Metadata.GetMetadata<RequiresSubscriptionAttribute>()?.Feature ?? feature;
        if (feature is { } f) http.RequestServices.GetRequiredService<ISubscriptionGuard>().Require(f);
        return await next(context);
    }
}

public sealed class SubscriptionMaintenance(SaasDatabase database, ILogger<SubscriptionMaintenance> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var list = database.Open(Studio.MasterId);
                var ids = await list.Studios.Select(s => s.Id).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    using var db = database.Open(id);
                    using var tx = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
                    var studio = db.Studios.Single(s => s.Id == id);
                    PlanRules.Refresh(studio, DateTimeOffset.UtcNow);
                    await db.SaveChangesAsync(stoppingToken);
                    await tx.CommitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError(e, "Subscription maintenance failed; retrying next minute"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
