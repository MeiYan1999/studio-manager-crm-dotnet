using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCore.Models;
using StudioCore.Persistence;
using StudioCore.Services;

namespace StudioCore.Api;

public static partial class SaasEndpoints
{
    static partial void MapBilling(RouteGroupBuilder admin, WebApplication app)
    {
        admin.MapPost("/billing/checkout", (CheckoutRequest request, TenantContext tenant, SubscriptionBilling billing) =>
        {
            if (!Enum.TryParse<SubscriptionPlan>(request.Plan, out var plan) || !Enum.TryParse<BillingCycle>(request.BillingCycle, out var cycle))
                throw new ApiException(400, "Gói không hợp lệ.", "invalid_plan");
            return Results.Ok(billing.Checkout(tenant.TenantId, plan, cycle));
        });
        admin.MapGet("/billing/history", (SaasDatabase database, SubscriptionBilling billing) =>
        {
            using var db = database.Open();
            return Results.Ok(new { Configured = billing.Configured, Transactions = db.SubscriptionTransactions.AsNoTracking().AsEnumerable()
                .OrderByDescending(t => t.CreatedAt).Take(100).Select(t => new { t.Id, t.PaymentCode, Plan = t.Plan.ToString(),
                    BillingCycle = t.BillingCycle.ToString(), t.Amount, Status = t.Status.ToString(), t.CreatedAt, t.PaidAt }).ToArray() });
        });
        admin.MapGet("/billing/events", async (HttpContext context, TenantContext tenant, BillingEvents events, AdminSessionStore sessions) =>
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Accel-Buffering"] = "no";
            var version = events.Version(tenant.TenantId);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            await context.Response.WriteAsync(": connected\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            while (await timer.WaitForNextTickAsync(context.RequestAborted))
            {
                if (!sessions.IsAuthenticated(context)) break;
                var next = events.Version(tenant.TenantId);
                await context.Response.WriteAsync(next != version ? "event: subscription\ndata: {}\n\n" : ": heartbeat\n\n", context.RequestAborted);
                version = next;
                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
        });
        app.MapPost("/api/payments/sepay-webhook", async (HttpContext context, SubscriptionBilling billing) =>
        {
            if (context.Request.ContentLength > 65536) return Results.StatusCode(413);
            using var memory = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
            {
                if (memory.Length + read > 65536) return Results.StatusCode(413);
                memory.Write(buffer, 0, read);
            }
            var raw = memory.ToArray();
            billing.Verify(context.Request, raw);
            SePayNotification? message;
            try { message = JsonSerializer.Deserialize<SePayNotification>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch (JsonException) { throw new ApiException(400, "Webhook JSON không hợp lệ.", "invalid_webhook"); }
            if (message is null) throw new ApiException(400, "Thiếu nội dung webhook.", "invalid_webhook");
            billing.Process(message);
            return Results.Ok(new { success = true });
        });
    }
}
public sealed record CheckoutRequest(string Plan, string BillingCycle);
