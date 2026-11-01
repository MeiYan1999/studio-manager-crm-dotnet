using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCore.Models;
using StudioCore.Persistence;
using StudioCore.Services;

namespace StudioCore.Api;

public static partial class SaasEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/saas").AddEndpointFilter<AdminOnlyFilter>();
        var admin = group.MapGroup("").AddEndpointFilter<StudioAdminFilter>();
        group.MapGet("/status", (SaasDatabase database, DataStore store) =>
        {
            using var db = database.Open();
            var studio = db.Studios.Single(s => s.Id == db.TenantId);
            var effective = PlanRules.Effective(studio, DateTimeOffset.UtcNow);
            var used = studio.PcFilterMonth == PlanRules.Month(DateTimeOffset.UtcNow) ? studio.CurrentMonthPcFilterUsed : 0;
            return Results.Ok(new { studio.Id, studio.StudioCode, studio.Name, studio.Slug, studio.CustomDomain,
                studio.CustomDomainVerified, CurrentPlan = effective.ToString(), BillingCycle = studio.BillingCycle.ToString(),
                studio.SubscriptionExpiresAt, studio.IsTrialGift, MaxUsers = PlanRules.Users(effective),
                ActiveUsers = store.Read(d => d.Users.Count(u => u.IsActive)), MonthlyPcFilterQuota = PlanRules.FilterQuota(effective),
                CurrentMonthPcFilterUsed = used, QuickLocations = JsonSerializer.Deserialize<string[]>(studio.QuickLocationsJson),
                Features = Enum.GetValues<StudioFeature>().ToDictionary(f => f.ToString(), f => effective >= PlanRules.Required(f)) });
        });
        group.MapPost("/pc-filter/consume", (FilterUsageRequest request, SaasDatabase database, TenantContext tenant) =>
        {
            if (string.IsNullOrWhiteSpace(request.AlbumKey) || request.AlbumKey.Length > 100)
                throw new ApiException(400, "Thiếu mã album/lượt lọc.", "invalid_album_key");
            return database.Mutate(tenant.TenantId, (db, studio) =>
            {
                PlanRules.Refresh(studio, DateTimeOffset.UtcNow);
                var month = PlanRules.Month(DateTimeOffset.UtcNow);
                var key = month + ":" + request.AlbumKey;
                if (!db.Set<PcFilterUsage>().Any(u => u.Id == key))
                {
                    if (studio.MonthlyPcFilterQuota != -1 && studio.CurrentMonthPcFilterUsed >= studio.MonthlyPcFilterQuota)
                        throw new ApiException(403, "Gói Free đã dùng hết 5 album lọc ảnh tháng này. Nâng cấp Album Pro để lọc không giới hạn.", "quota_exceeded");
                    db.Set<PcFilterUsage>().Add(new PcFilterUsage { Id = key, Month = month });
                    studio.CurrentMonthPcFilterUsed++;
                }
                return Results.Ok(new { studio.CurrentMonthPcFilterUsed, studio.MonthlyPcFilterQuota });
            });
        });
        MapBilling(admin, app);
        MapSettings(admin);
    }
    static partial void MapBilling(RouteGroupBuilder admin, WebApplication app);
    static partial void MapSettings(RouteGroupBuilder admin);
}
public sealed record FilterUsageRequest(string AlbumKey);
