using System.Data;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using StudioCore.Models;

namespace StudioCore.Persistence;

public sealed class SaasDatabase(IWebHostEnvironment environment, IConfiguration configuration, TenantContext tenant)
{
    private readonly object sync = new();
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    public string DataDirectory => Path.GetFullPath(configuration["Data:Directory"] ?? "data", environment.ContentRootPath);
    public StudioDbContext Open(string? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<StudioDbContext>();
        var connection = configuration.GetConnectionString("Postgres") ?? configuration["DATABASE_URL"] ?? "";
        if (connection.Length > 0) options.UseNpgsql(Services.DataStore.NormalizePostgresConnectionString(connection));
        else { Directory.CreateDirectory(DataDirectory); options.UseSqlite("Data Source=" + Path.Combine(DataDirectory, "saas.db")); }
        // Sensitive-data logging is intentionally never enabled.
        return new StudioDbContext(options.Options, tenantId ?? tenant.TenantId);
    }

    public void Initialize(AppData legacy)
    {
        lock (sync)
        {
            using var db = Open(Studio.MasterId);
            db.Database.Migrate();
            using var tx = db.Database.BeginTransaction(IsolationLevel.Serializable);
            if (db.Workspaces.Any()) return; // One-time import; source JSON/app_state is retained.
            var master = db.Studios.Find(Studio.MasterId);
            if (master is null)
            {
                // Existing studios keep their current features through an explicit legacy gift.
                // New studios always start Free. Never grant this exception by request input.
                db.Studios.Add(new Studio { Id = Studio.MasterId, Name = legacy.Settings.StudioName,
                    CurrentPlan = SubscriptionPlan.ErpStudio, IsTrialGift = true, MaxUsers = 5, MonthlyPcFilterQuota = -1 });
                db.SaveChanges();
            }
            Stamp(legacy, Studio.MasterId);
            var workspace = new TenantWorkspace { TenantId = Studio.MasterId };
            db.Workspaces.Add(workspace);
            Persist(db, legacy, workspace);
            tx.Commit();
        }
    }

    public T Read<T>(Func<AppData, T> reader)
    {
        lock (sync) { using var db = Open(); using var tx = db.Database.BeginTransaction(IsolationLevel.Serializable); return reader(Load(db)); }
    }
    public T Write<T>(Func<AppData, T> writer)
    {
        lock (sync)
        {
            using var db = Open();
            using var tx = db.Database.BeginTransaction(IsolationLevel.Serializable);
            if (db.Database.IsNpgsql())
                db.Database.ExecuteSqlInterpolated($"SELECT 1 FROM \"TenantWorkspace\" WHERE \"TenantId\" = {db.TenantId} AND \"Id\" = 'state' FOR UPDATE");
            var data = Load(db);
            var previousActive = data.Users.Count(u => u.IsActive);
            var result = writer(data); // Exceptions leave both database and in-memory data unchanged.
            var studio = db.Studios.Single(s => s.Id == db.TenantId);
            var active = data.Users.Count(u => u.IsActive);
            studio.Name = data.Settings.StudioName;
            var pending = db.Set<StudioInvitation>().AsEnumerable().Count(i => i.AcceptedAt is null && i.ExpiresAt > DateTimeOffset.UtcNow);
            if (active > previousActive && active + pending > Services.PlanRules.Users(Services.PlanRules.Effective(studio, DateTimeOffset.UtcNow)))
                throw new ApiException(409, "Đã hết slot user. Vui lòng nâng cấp gói hoặc vô hiệu hóa tài khoản cũ.", "user_slots_full");
            Stamp(data, db.TenantId);
            Persist(db, data, db.Workspaces.Single());
            tx.Commit();
            return result;
        }
    }

    private AppData Load(StudioDbContext db)
    {
        var workspace = db.Workspaces.SingleOrDefault() ?? throw new ApiException(404, "Studio chưa được khởi tạo.", "tenant_not_found");
        var data = JsonSerializer.Deserialize<AppData>(workspace.MetadataJson, json)!;
        using var metadata = JsonDocument.Parse(workspace.MetadataJson);
        metadata.RootElement.TryGetProperty("collectionOrder", out var order);
        foreach (var property in StudioDbContext.Collections)
        {
            var method = typeof(SaasDatabase).GetMethod(nameof(LoadList), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            var keys = order.ValueKind == JsonValueKind.Object && order.TryGetProperty(property.Name, out var ids)
                ? ids.Deserialize<string[]>() : null;
            property.SetValue(data, method.MakeGenericMethod(property.PropertyType.GenericTypeArguments[0]).Invoke(null, [db, keys]));
        }
        return data;
    }
    private static List<T> LoadList<T>(StudioDbContext db, string[]? order) where T : class
    {
        var rows = db.Set<T>().ToList();
        if (order is null) return rows;
        var positions = order.Select((id, index) => (id, index)).ToDictionary(p => p.id, p => p.index);
        var key = typeof(T).GetProperty("Id")!;
        return rows.OrderBy(row => positions.GetValueOrDefault((string)key.GetValue(row)!, int.MaxValue)).ToList();
    }
    private void Persist(StudioDbContext db, AppData data, TenantWorkspace workspace)
    {
        var metadata = JsonSerializer.Deserialize<AppData>(JsonSerializer.Serialize(data, json), json)!;
        var order = new Dictionary<string, string[]>();
        foreach (var property in StudioDbContext.Collections)
        {
            var type = property.PropertyType.GenericTypeArguments[0];
            order[property.Name] = ((IEnumerable)property.GetValue(data)!).Cast<object>().Select(row => (string)type.GetProperty("Id")!.GetValue(row)!).ToArray();
            typeof(SaasDatabase).GetMethod(nameof(SaveList), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                .MakeGenericMethod(type).Invoke(null, [db, property.GetValue(data)!]);
            property.SetValue(metadata, Activator.CreateInstance(property.PropertyType));
        }
        var node = JsonSerializer.SerializeToNode(metadata, json)!;
        node["collectionOrder"] = JsonSerializer.SerializeToNode(order, json);
        workspace.MetadataJson = node.ToJsonString(json);
        workspace.Revision++;
        db.SaveChanges();
    }
    private static void SaveList<T>(StudioDbContext db, List<T> rows) where T : class, IMustHaveTenant
    {
        var id = typeof(T).GetProperty("Id")!;
        var incoming = rows.ToDictionary(x => (string)id.GetValue(x)!);
        var existing = db.Set<T>().ToList();
        foreach (var row in existing)
        {
            var key = (string)id.GetValue(row)!;
            if (!incoming.Remove(key, out var updated)) db.Remove(row);
            else if (!ReferenceEquals(row, updated)) db.Entry(row).CurrentValues.SetValues(updated);
        }
        db.AddRange(incoming.Values);
    }

    public T Mutate<T>(string tenantId, Func<StudioDbContext, Studio, T> action)
    {
        lock (sync)
        {
            using var db = Open(tenantId);
            using var tx = db.Database.BeginTransaction(IsolationLevel.Serializable);
            if (db.Database.IsNpgsql())
                db.Database.ExecuteSqlInterpolated($"SELECT 1 FROM \"Studios\" WHERE \"Id\" = {tenantId} FOR UPDATE");
            var studio = db.Studios.Single(s => s.Id == tenantId);
            var result = action(db, studio);
            db.SaveChanges();
            tx.Commit();
            return result;
        }
    }

    public static void Stamp(object? value, string tenantId)
    {
        if (value is null || value is string || value.GetType().IsValueType) return;
        if (value is IMustHaveTenant owned)
        {
            if (owned.TenantId.Length > 0 && owned.TenantId != tenantId)
                throw new ApiException(403, "Dữ liệu không thuộc studio hiện tại.", "tenant_mismatch");
            owned.TenantId = tenantId;
        }
        if (value is IEnumerable list) { foreach (var item in list) Stamp(item, tenantId); return; }
        foreach (var p in value.GetType().GetProperties().Where(p => p.CanWrite)) Stamp(p.GetValue(value), tenantId);
    }
}
