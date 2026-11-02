using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StudioCore.Models;
using StudioCore.Persistence;

namespace StudioCore.Services;

// Operator-only provisioning; never accepts a plan or admin role from public request input.
public sealed class StudioProvisioning(SaasDatabase database)
{
    public ProvisionedStudio Create(string name, string slug, string username, string password, string email,
        bool requireEmailVerification = false, string? googleSubject = null, string? displayName = null)
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
        var owner = new StudioUser { Username = username, DisplayName = displayName?.Trim() is { Length: > 0 } ownerName ? ownerName : studio.Name, Email = email,
            GoogleSubject = googleSubject,
            Role = "admin", PasswordHash = PasswordHasher.Hash(password), IsEmailVerified = !requireEmailVerification,
            PermissionsConfigured = true, Permissions = StudioPermissions.All.ToList() };
        var token = requireEmailVerification ? AccountTokens.NewToken() : "";
        db.Add(owner);
        if (requireEmailVerification)
            db.Add(new AccountToken { UserId = owner.Id, Purpose = AccountTokens.VerifyEmail,
                TokenHash = AccountTokens.Hash(token), ExpiresAt = DateTimeOffset.UtcNow.AddHours(24) });
        db.SaveChanges(); tx.Commit(); return new ProvisionedStudio(studio, token);
    }

    public ProvisionedStudio CreateFromGoogle(string subject, string email, string displayName)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Google không trả về đủ thông tin tài khoản.");

        var studioName = string.IsNullOrWhiteSpace(displayName) ? "Studio mới" : displayName.Trim() + " Studio";
        var slugBase = Slugify(string.IsNullOrWhiteSpace(displayName) ? email.Split('@')[0] : displayName);
        if (slugBase.Length < 3) slugBase = "studio";
        var usernameBase = Regex.Replace(email.Split('@')[0].ToLowerInvariant(), "[^a-z0-9._-]", "").Trim('.', '_', '-');
        if (usernameBase.Length < 3) usernameBase = "owner";
        if (usernameBase.Length > 24) usernameBase = usernameBase[..24];

        for (var attempt = 0; attempt < 12; attempt++)
        {
            var suffix = attempt == 0 ? "" : "-" + RandomNumberGenerator.GetInt32(1000, 9999);
            var slug = (slugBase.Length + suffix.Length > 63 ? slugBase[..(63 - suffix.Length)] : slugBase) + suffix;
            var username = attempt == 0 ? usernameBase : usernameBase + RandomNumberGenerator.GetInt32(10, 99);
            try
            {
                return Create(studioName, slug, username, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                    email.Trim().ToLowerInvariant(), googleSubject: subject, displayName: displayName);
            }
            catch (DbUpdateException)
            {
                using var lookup = database.Open(Studio.MasterId);
                if (lookup.Set<StudioUser>().IgnoreQueryFilters().Any(user => user.GoogleSubject == subject) || attempt == 11)
                    throw;
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("Slug", StringComparison.OrdinalIgnoreCase) && attempt < 11) { }
        }

        throw new InvalidOperationException("Chưa thể tạo định danh studio. Vui lòng thử lại.");
    }

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var ascii = new string(normalized.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var slug = Regex.Replace(ascii, "[^a-z0-9]+", "-").Trim('-');
        return slug.Length > 54 ? slug[..54].TrimEnd('-') : slug;
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
