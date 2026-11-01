using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StudioCore.Models;
using StudioCore.Persistence;

namespace StudioCore.Services;

public sealed class BillingEvents
{
    private readonly ConcurrentDictionary<string, long> revisions = new();
    public long Version(string tenantId) => revisions.GetValueOrDefault(tenantId);
    public void Publish(string tenantId) => revisions.AddOrUpdate(tenantId, 1, (_, old) => old + 1);
}

public sealed class SubscriptionBilling(SaasDatabase database, IConfiguration configuration, BillingEvents events)
{
    public bool Configured => !string.IsNullOrWhiteSpace(configuration["Billing:BankCode"]) &&
        !string.IsNullOrWhiteSpace(configuration["Billing:AccountNumber"]) &&
        (!string.IsNullOrWhiteSpace(configuration["SePay:WebhookSecret"]) || !string.IsNullOrWhiteSpace(configuration["SePay:ApiKey"]));

    public object Checkout(string tenantId, SubscriptionPlan plan, BillingCycle cycle)
    {
        if (!Configured) throw new ApiException(503, "Chưa cấu hình ngân hàng và xác thực SePay. Vui lòng liên hệ quản trị nền tảng.", "billing_not_configured");
        if (!Enum.IsDefined(plan) || !Enum.IsDefined(cycle) || plan == SubscriptionPlan.Free)
            throw new ApiException(400, "Gói hoặc chu kỳ không hợp lệ.", "invalid_plan");
        return database.Mutate(tenantId, (db, studio) =>
        {
            var effective = PlanRules.Effective(studio, DateTimeOffset.UtcNow);
            if (plan < effective) throw new ApiException(409, "Có thể chuyển xuống gói thấp hơn sau khi gói hiện tại hết hạn.", "downgrade_pending");
            var amount = PlanRules.Price(plan, cycle);
            var transaction = db.SubscriptionTransactions.AsEnumerable().FirstOrDefault(t => t.Plan == plan && t.BillingCycle == cycle &&
                t.Amount == amount && t.Status == SubscriptionPaymentStatus.Pending && t.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-30));
            if (transaction is null)
            {
                transaction = new SubscriptionTransaction { Plan = plan, BillingCycle = cycle, Amount = amount,
                    PaymentCode = "SC" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)), TenantId = tenantId };
                db.SubscriptionTransactions.Add(transaction);
            }
            var description = $"UPGRADE {studio.StudioCode} {plan.ToString().ToUpperInvariant()} {transaction.PaymentCode}";
            var bank = configuration["Billing:BankCode"]!;
            var account = configuration["Billing:AccountNumber"]!;
            var qr = "https://qr.sepay.vn/img?acc=" + Uri.EscapeDataString(account) + "&bank=" + Uri.EscapeDataString(bank)
                + "&amount=" + transaction.Amount.ToString("0", CultureInfo.InvariantCulture) + "&des=" + Uri.EscapeDataString(description);
            return new { transaction.Id, transaction.PaymentCode, transaction.Amount, Plan = plan.ToString(), BillingCycle = cycle.ToString(),
                Description = description, Bank = bank, AccountNumber = account, AccountName = configuration["Billing:AccountName"] ?? "", QrUrl = qr,
                ExpiresAt = transaction.CreatedAt.AddMinutes(30) };
        });
    }

    public void Verify(HttpRequest request, byte[] rawBody)
    {
        if (!Configured) throw new ApiException(503, "Webhook chưa được cấu hình.", "billing_not_configured");
        var secret = configuration["SePay:WebhookSecret"];
        var valid = false;
        if (!string.IsNullOrWhiteSpace(secret))
        {
            var timestamp = request.Headers["X-SePay-Timestamp"].ToString();
            if (long.TryParse(timestamp, out var unix) && Math.Abs((decimal)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - unix) <= 300)
            {
                var signed = Encoding.UTF8.GetBytes(timestamp + ".").Concat(rawBody).ToArray();
                var expected = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed)).ToLowerInvariant();
                valid = Equal(expected, request.Headers["X-SePay-Signature"].ToString());
            }
        }
        else valid = Equal("Apikey " + configuration["SePay:ApiKey"], request.Headers.Authorization.ToString());
        if (!valid) throw new ApiException(401, "Xác thực webhook không hợp lệ.", "invalid_webhook_signature");
    }
    private static bool Equal(string a, string b) => CryptographicOperations.FixedTimeEquals(
        SHA256.HashData(Encoding.UTF8.GetBytes(a)), SHA256.HashData(Encoding.UTF8.GetBytes(b)));

    public void Process(SePayNotification message)
    {
        if (message.Id <= 0 || message.TransferType != "in") return;
        if (message.AccountNumber != configuration["Billing:AccountNumber"])
            throw new ApiException(422, "Tài khoản nhận không khớp.", "payment_account_mismatch");
        using var lookup = database.Open(Studio.MasterId);
        var reference = "sepay:" + message.Id.ToString(CultureInfo.InvariantCulture);
        if (lookup.SubscriptionTransactions.IgnoreQueryFilters().Any(t => t.SePayReference == reference)) return;
        var tokens = Regex.Matches((message.Content ?? "").ToUpperInvariant(), "[A-Z0-9]+").Select(m => m.Value).ToHashSet();
        var codes = tokens.Where(t => t.StartsWith("SC", StringComparison.Ordinal) && t.Length == 18).ToArray();
        var payment = lookup.SubscriptionTransactions.IgnoreQueryFilters().SingleOrDefault(t => codes.Contains(t.PaymentCode));
        if (payment is null) return; // A normal bank transfer, unrelated to a pending upgrade.
        database.Mutate(payment.TenantId, (db, studio) =>
        {
            var transaction = db.SubscriptionTransactions.Single(t => t.Id == payment.Id);
            if (transaction.Status == SubscriptionPaymentStatus.Paid) return false;
            if (transaction.Status != SubscriptionPaymentStatus.Pending || transaction.Amount != message.TransferAmount ||
                !tokens.Contains(studio.StudioCode) || !tokens.Contains(transaction.Plan.ToString().ToUpperInvariant()))
                throw new ApiException(422, "Nội dung hoặc số tiền không khớp đơn nâng cấp.", "payment_mismatch");
            // Late transfers remain recorded; no expiry can make already transferred money vanish.
            transaction.Status = SubscriptionPaymentStatus.Paid;
            transaction.SePayReference = reference;
            transaction.PaidAt = DateTimeOffset.UtcNow;
            var effective = PlanRules.Effective(studio, DateTimeOffset.UtcNow);
            if (effective > transaction.Plan)
            { transaction.Status = SubscriptionPaymentStatus.ManualReview; return false; }
            var start = effective == transaction.Plan && studio.SubscriptionExpiresAt > DateTimeOffset.UtcNow
                ? studio.SubscriptionExpiresAt.Value : DateTimeOffset.UtcNow;
            studio.CurrentPlan = transaction.Plan;
            studio.BillingCycle = transaction.BillingCycle;
            studio.SubscriptionExpiresAt = transaction.BillingCycle == BillingCycle.Yearly ? start.AddYears(1) : start.AddMonths(1);
            studio.IsTrialGift = false;
            PlanRules.Refresh(studio, DateTimeOffset.UtcNow);
            return true;
        });
        events.Publish(payment.TenantId);
    }
}

public sealed class SePayNotification
{
    public long Id { get; set; }
    public string TransferType { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string Content { get; set; } = "";
    public decimal TransferAmount { get; set; }
}
