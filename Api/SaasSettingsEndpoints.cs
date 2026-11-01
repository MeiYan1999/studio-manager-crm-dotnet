using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StudioCore.Models;
using StudioCore.Persistence;
using StudioCore.Services;

namespace StudioCore.Api;

public static partial class SaasEndpoints
{
    private static string NormalizeSlug(string? value)
    {
        var slug = (value ?? "").Trim().ToLowerInvariant();
        if (!Regex.IsMatch(slug, "^[a-z0-9](?:[a-z0-9-]{1,61}[a-z0-9])$") || new[] { "www", "api", "cname", "admin", "mail", "support", "system" }.Contains(slug))
            throw new ApiException(400, "Slug cần 3–63 ký tự chữ thường, số hoặc dấu gạch ngang.", "invalid_slug");
        return slug;
    }
    static partial void MapSettings(RouteGroupBuilder admin)
    {
        admin.MapGet("/domains", (SaasDatabase database, IConfiguration config) =>
        {
            using var db = database.Open(); var s = db.Studios.Single(s => s.Id == db.TenantId);
            var domain = (config["Saas:BaseDomain"] ?? config["RENDER_EXTERNAL_HOSTNAME"] ?? "localhost").Trim().ToLowerInvariant();
            return Results.Ok(new { BaseDomain = domain, s.Slug, s.CustomDomain, s.CustomDomainVerified,
                VerificationName = s.CustomDomain is null ? "" : "_studiocore." + s.CustomDomain, s.DomainVerificationToken,
                CnameTarget = "cname." + domain, StandardUrl = s.Slug is null ? "" : "https://www." + domain + "/s/" + s.Slug,
                SubdomainUrl = s.Slug is null ? "" : "https://" + s.Slug + "." + domain });
        });
        admin.MapGet("/domains/slug-available", (string slug, SaasDatabase database) =>
        {
            slug = NormalizeSlug(slug); using var db = database.Open();
            return Results.Ok(new { Slug = slug, Available = !db.Studios.Any(s => s.Slug == slug && s.Id != db.TenantId) });
        });
        admin.MapPut("/domains", (DomainRequest request, SaasDatabase database, TenantContext tenant, ISubscriptionGuard guard, IConfiguration config) =>
        {
            var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : NormalizeSlug(request.Slug);
            string? domain = null;
            if (!string.IsNullOrWhiteSpace(request.CustomDomain))
            {
                guard.Require(StudioFeature.CustomDomain);
                try { domain = new IdnMapping().GetAscii(request.CustomDomain.Trim().TrimEnd('.')).ToLowerInvariant(); }
                catch (ArgumentException) { throw new ApiException(400, "Tên miền không hợp lệ.", "invalid_domain"); }
                var platform = (config["Saas:BaseDomain"] ?? config["RENDER_EXTERNAL_HOSTNAME"] ?? "localhost").ToLowerInvariant();
                if (domain.Length > 253 || !Regex.IsMatch(domain, "^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\\.)+[a-z]{2,63}$") ||
                    domain == platform || domain.EndsWith("." + platform, StringComparison.Ordinal))
                    throw new ApiException(400, "Nhập tên miền riêng, không kèm https, cổng hoặc đường dẫn.", "invalid_domain");
            }
            return database.Mutate(tenant.TenantId, (db, studio) =>
            {
                if (studio.Slug is not null && slug is null)
                    throw new ApiException(400, "Studio đã có đường dẫn. Hãy nhập slug mới thay vì để trống.", "slug_required");
                // An expired studio can edit its free slug without losing its saved custom domain.
                if (request.CustomDomain is null) domain = studio.CustomDomain;
                if (db.Studios.Any(s => s.Id != studio.Id && ((slug != null && s.Slug == slug) || (domain != null && s.CustomDomain == domain))))
                    throw new ApiException(409, "Slug hoặc tên miền đã được studio khác sử dụng.", "domain_taken");
                if (studio.CustomDomain != domain)
                { studio.CustomDomainVerified = false; studio.DomainVerificationToken = Guid.NewGuid().ToString("N"); }
                studio.Slug = slug; studio.CustomDomain = domain;
                return Results.Ok(new { studio.Slug, studio.CustomDomain, studio.CustomDomainVerified });
            });
        });
        admin.MapPost("/domains/verify", async (SaasDatabase database, TenantContext tenant, ISubscriptionGuard guard, IHttpClientFactory factory, IConfiguration config, CancellationToken ct) =>
        {
            guard.Require(StudioFeature.CustomDomain);
            using var db = database.Open(); var studio = db.Studios.Single(s => s.Id == db.TenantId);
            if (studio.CustomDomain is null) throw new ApiException(400, "Hãy lưu tên miền trước khi xác minh.", "missing_domain");
            var client = factory.CreateClient("dns");
            async Task<string[]> Lookup(string name, string type)
            {
                using var response = await client.GetAsync("https://cloudflare-dns.com/dns-query?name=" + Uri.EscapeDataString(name) + "&type=" + type, ct);
                response.EnsureSuccessStatusCode();
                using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                return payload.RootElement.TryGetProperty("Answer", out var values)
                    ? values.EnumerateArray().Where(v => v.GetProperty("type").GetInt32() == (type == "CNAME" ? 5 : 16)).Select(v => v.GetProperty("data").GetString() ?? "").ToArray() : [];
            }
            var cname = await Lookup(studio.CustomDomain, "CNAME");
            var txt = await Lookup("_studiocore." + studio.CustomDomain, "TXT");
            var target = "cname." + (config["Saas:BaseDomain"] ?? config["RENDER_EXTERNAL_HOSTNAME"] ?? "localhost").ToLowerInvariant();
            var verified = cname.Any(v => v.TrimEnd('.').Equals(target, StringComparison.OrdinalIgnoreCase)) && txt.Any(v => v.Trim('"') == studio.DomainVerificationToken);
            return database.Mutate(tenant.TenantId, (_, current) =>
            {
                if (current.CustomDomain != studio.CustomDomain || current.DomainVerificationToken != studio.DomainVerificationToken)
                    throw new ApiException(409, "Tên miền đã thay đổi. Hãy xác minh lại.", "domain_changed");
                current.CustomDomainVerified = verified;
                return Results.Ok(new { Verified = verified, Message = verified ? "Đã xác minh CNAME và quyền sở hữu tên miền." : "Chưa tìm thấy CNAME và TXT khớp. Kiểm tra DNS rồi thử lại." });
            });
        });
        admin.MapPut("/locations", (LocationsRequest request, SaasDatabase database, TenantContext tenant) =>
        {
            var locations = (request.Locations ?? []).Select(v => (v ?? "").Trim()).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (locations.Length > 30 || locations.Any(v => v.Length > 80)) throw new ApiException(400, "Tối đa 30 địa điểm, mỗi tên tối đa 80 ký tự.", "invalid_locations");
            return database.Mutate(tenant.TenantId, (_, studio) => { studio.QuickLocationsJson = JsonSerializer.Serialize(locations); return Results.Ok(locations); });
        });
        admin.MapGet("/preferences", (DataStore store) => Results.Ok(store.Read(d => new { d.Settings.ServiceTypes, d.Settings.ShareMessage, d.Settings.Greeting, d.Settings.ContractTemplate })));
        admin.MapPut("/preferences", (PreferencesRequest request, DataStore store, ISubscriptionGuard guard) =>
        {
            if (request.ContractTemplate is not null) guard.Require(StudioFeature.Crm);
            if ((request.ShareMessage?.Length ?? 0) > 3000 || (request.Greeting?.Length ?? 0) > 3000 || (request.ContractTemplate?.Length ?? 0) > 20000)
                throw new ApiException(400, "Nội dung quá dài.", "invalid_preferences");
            if (request.ServiceTypes is { } types && (types.Count > 30 || types.Any(t => t is null || string.IsNullOrWhiteSpace(t.Name) || t.Name.Length > 80 || t.Color is null || !Regex.IsMatch(t.Color, "^#[0-9A-Fa-f]{6}$"))))
                throw new ApiException(400, "Loại dịch vụ hoặc mã màu không hợp lệ.", "invalid_service_type");
            store.Write(d =>
            {
                if (request.ServiceTypes is not null) d.Settings.ServiceTypes = request.ServiceTypes;
                if (request.ShareMessage is not null) d.Settings.ShareMessage = request.ShareMessage;
                if (request.Greeting is not null) d.Settings.Greeting = request.Greeting;
                if (request.ContractTemplate is not null) d.Settings.ContractTemplate = request.ContractTemplate;
            });
            return Results.Ok(new { Saved = true });
        });
        admin.MapGet("/backup/albums", (DataStore store, ISubscriptionGuard guard) =>
        {
            guard.Require(StudioFeature.Backup);
            return Results.Json(store.Read(d => new AlbumBackup(1, d.Albums, d.PriceCatalog, d.Concepts)), contentType: "application/json");
        });
        admin.MapPost("/backup/albums/restore", (AlbumBackup request, DataStore store, ISubscriptionGuard guard) =>
        {
            guard.Require(StudioFeature.Backup);
            if (request.Version != 1 || request.Albums is null || request.Albums.Count > 5000 || request.PriceCatalog is null || request.Concepts is null)
                throw new ApiException(400, "Bản sao lưu không hợp lệ.", "invalid_backup");
            store.Write(d =>
            {
                foreach (var album in request.Albums)
                {
                    if (string.IsNullOrWhiteSpace(album.Id) || string.IsNullOrWhiteSpace(album.ShareToken)) throw new ApiException(400, "Album thiếu mã.", "invalid_backup");
                    var index = d.Albums.FindIndex(a => a.Id == album.Id);
                    if (index < 0) d.Albums.Add(album); else d.Albums[index] = album;
                }
                foreach (var group in request.PriceCatalog) { var i = d.PriceCatalog.FindIndex(g => g.Id == group.Id); if (i < 0) d.PriceCatalog.Add(group); else d.PriceCatalog[i] = group; }
                foreach (var concept in request.Concepts) { var i = d.Concepts.FindIndex(c => c.Id == concept.Id); if (i < 0) d.Concepts.Add(concept); else d.Concepts[i] = concept; }
            });
            return Results.Ok(new { Restored = request.Albums.Count });
        });
    }
}
public sealed record DomainRequest(string? Slug, string? CustomDomain);
public sealed record LocationsRequest(string[]? Locations);
public sealed record PreferencesRequest(List<StudioServiceType>? ServiceTypes, string? ShareMessage, string? Greeting, string? ContractTemplate);
public sealed record AlbumBackup(int Version, List<Album> Albums, List<PriceGroup> PriceCatalog, List<StudioConcept> Concepts);
