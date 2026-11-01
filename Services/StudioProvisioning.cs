using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using StudioCore.Models;
using StudioCore.Persistence;

namespace StudioCore.Services;

// Operator-only provisioning; never accepts a plan or admin role from public request input.
public sealed class StudioProvisioning(SaasDatabase database)
{
    public ProvisionedStudio Create(string name, string slug, string username, string password, string email, bool requireEmailVerification = false)
    {
        slug = slug.Trim().ToLowerInvariant(); username = username.Trim().ToLowerInvariant();
        if (!Regex.IsMatch(slug, "^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$") || new[] { "www", "api", "admin", "cname", "mail", "support", "system" }.Contains(slug) ||
            !Regex.IsMatch(username, "^[a-z0-9._-]{3,32}$") || password.Length is < 8 or > 128 || string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Tên studio, slug, username hoặc mật khẩu provisioning không hợp lệ.");
        var studio = new Studio { Name = name.Trim(), Slug = slug };
        using var db = database.Open(studio.Id);
        using var tx = db.Database.BeginTransaction();
        if (db.Studios.Any(s => s.Slug == slug)) throw new InvalidOperationException("Slug đã được sử dụng.");
        db.Studios.Add(studio); db.SaveChanges();
        var data = new AppData { SchemaVersion = 8, Settings = new StudioSettings { StudioName = studio.Name, Website = new WebsiteContent { BrandName = studio.Name } }, PriceCatalog = [] };
        db.Workspaces.Add(new TenantWorkspace { MetadataJson = JsonSerializer.Serialize(data, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        var owner = new StudioUser { Username = username, DisplayName = studio.Name, Email = email,
            Role = "admin", PasswordHash = PasswordHasher.Hash(password), IsEmailVerified = !requireEmailVerification,
            PermissionsConfigured = true, Permissions = StudioPermissions.All.ToList() };
        var token = requireEmailVerification ? AccountTokens.NewToken() : "";
        db.Add(owner);
        if (requireEmailVerification)
            db.Add(new AccountToken { UserId = owner.Id, Purpose = AccountTokens.VerifyEmail,
                TokenHash = AccountTokens.Hash(token), ExpiresAt = DateTimeOffset.UtcNow.AddHours(24) });
        db.SaveChanges(); tx.Commit(); return new ProvisionedStudio(studio, token);
    }
}

public sealed record ProvisionedStudio(Studio Studio, string VerificationToken);

public static class AccountTokens
{
    public const string VerifyEmail = "verify-email";
    public const string ResetPassword = "reset-password";
    public static string NewToken() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public static string Hash(string token) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
