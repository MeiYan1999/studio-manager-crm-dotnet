using System.Text.Json;

namespace StudioCore.Models;

public interface IMustHaveTenant { string TenantId { get; set; } }
public enum SubscriptionPlan { Free, AlbumPro, ErpSolo, ErpStudio }
public enum BillingCycle { Monthly, Yearly }
public enum SubscriptionPaymentStatus { Pending, Paid, Cancelled, ManualReview }

public sealed class GoogleIdentity
{
    public string GoogleSubject { get; set; } = "";
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastLoginAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Studio
{
    public const string MasterId = "system-master";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string StudioCode { get; set; } = NewCode();
    public string Name { get; set; } = "StudioCore";
    public string? Slug { get; set; }
    public string? CustomDomain { get; set; }
    public bool CustomDomainVerified { get; set; }
    public string DomainVerificationToken { get; set; } = Guid.NewGuid().ToString("N");
    public SubscriptionPlan CurrentPlan { get; set; }
    public BillingCycle BillingCycle { get; set; }
    public DateTimeOffset? SubscriptionExpiresAt { get; set; }
    public bool IsTrialGift { get; set; }
    public int MaxUsers { get; set; } = 1;
    public int MonthlyPcFilterQuota { get; set; } = 5;
    public int CurrentMonthPcFilterUsed { get; set; }
    public string PcFilterMonth { get; set; } = "";
    public string QuickLocationsJson { get; set; } = JsonSerializer.Serialize(DefaultLocations);
    public static readonly string[] DefaultLocations = ["Phòng Studio A", "Phòng Studio B", "Ngoại cảnh", "Phim trường"];
    public static string NewCode() => new(Enumerable.Range(0, 12).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[System.Security.Cryptography.RandomNumberGenerator.GetInt32(36)]).ToArray());
}

public sealed class TenantWorkspace : IMustHaveTenant
{
    public string TenantId { get; set; } = "";
    public string Id { get; set; } = "state";
    public string MetadataJson { get; set; } = "{}";
    public long Revision { get; set; }
}

public sealed class SubscriptionTransaction : IMustHaveTenant
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = "";
    public SubscriptionPlan Plan { get; set; }
    public BillingCycle BillingCycle { get; set; }
    public decimal Amount { get; set; }
    public string PaymentCode { get; set; } = "";
    public SubscriptionPaymentStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaidAt { get; set; }
    public string? SePayReference { get; set; }
}

public sealed class PcFilterUsage : IMustHaveTenant
{
    public string TenantId { get; set; } = "";
    public string Id { get; set; } = "";
    public string Month { get; set; } = "";
}

public sealed class StudioInvitation : IMustHaveTenant
{
    public string TenantId { get; set; } = "";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Email { get; set; } = "";
    public string? PendingKey { get; set; }
    public string Role { get; set; } = "employee";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}

public sealed class AccountToken : IMustHaveTenant
{
    public string TenantId { get; set; } = "";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
