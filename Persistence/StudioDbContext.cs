using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StudioCore.Models;

namespace StudioCore.Persistence;

public sealed class StudioDbContext(DbContextOptions<StudioDbContext> options, string tenantId) : DbContext(options)
{
    public string TenantId { get; } = tenantId;
    public DbSet<Studio> Studios => Set<Studio>();
    public DbSet<SubscriptionTransaction> SubscriptionTransactions => Set<SubscriptionTransaction>();
    public DbSet<TenantWorkspace> Workspaces => Set<TenantWorkspace>();
    public static readonly PropertyInfo[] Collections = typeof(AppData).GetProperties()
        .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(List<>)).ToArray();
    public static readonly Type[] TenantTypes = Collections.Select(p => p.PropertyType.GenericTypeArguments[0])
        .Concat([typeof(TenantWorkspace), typeof(SubscriptionTransaction), typeof(PcFilterUsage), typeof(StudioInvitation), typeof(AccountToken)]).ToArray();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Studio>(e =>
        {
            e.ToTable("Studios");
            e.HasKey(s => s.Id);
            e.Property(s => s.StudioCode).HasMaxLength(12);
            e.HasIndex(s => s.StudioCode).IsUnique();
            e.HasIndex(s => s.Slug).IsUnique();
            e.HasIndex(s => s.CustomDomain).IsUnique();
        });
        foreach (var type in TenantTypes)
            typeof(StudioDbContext).GetMethod(nameof(ConfigureTenant), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(type).Invoke(this, [model]);
        model.Entity<TenantWorkspace>().Property(s => s.Revision).IsConcurrencyToken();
        model.Entity<SubscriptionTransaction>().HasIndex(s => s.PaymentCode).IsUnique();
        model.Entity<SubscriptionTransaction>().HasIndex(s => s.SePayReference).IsUnique();
        model.Entity<StudioUser>().HasIndex(s => new { s.TenantId, s.Username }).IsUnique();
        model.Entity<AccountToken>().HasIndex(s => s.TokenHash).IsUnique();
        model.Entity<AccountToken>().HasIndex(s => new { s.TenantId, s.UserId, s.Purpose });
        model.Entity<Album>().HasIndex(s => new { s.TenantId, s.ShareToken }).IsUnique();
    }

    private void ConfigureTenant<T>(ModelBuilder model) where T : class, IMustHaveTenant
    {
        var e = model.Entity<T>();
        e.ToTable(typeof(T).Name);
        e.HasKey("TenantId", "Id");
        e.Property(x => x.TenantId).HasDefaultValue(Studio.MasterId);
        e.HasQueryFilter(x => x.TenantId == TenantId);
        e.HasOne<Studio>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        foreach (var p in typeof(T).GetProperties())
        {
            if (!p.CanWrite) { e.Ignore(p.Name); continue; }
            if (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
            {
                // Nested photo/selection/items remain JSON values, preserving their existing shape.
                typeof(StudioDbContext).GetMethod(nameof(JsonProperty), BindingFlags.Static | BindingFlags.NonPublic)!
                    .MakeGenericMethod(p.PropertyType).Invoke(null, [model, typeof(T), p.Name]);
            }
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T Decode<T>(string value) => JsonSerializer.Deserialize<T>(value, Json)!;
    private static void JsonProperty<T>(ModelBuilder model, Type entity, string name)
    {
        var property = model.Entity(entity).Property<T>(name);
        property.HasConversion(new ValueConverter<T, string>(v => Encode(v), v => Decode<T>(v)));
        property.Metadata.SetValueComparer(new ValueComparer<T>(
            (a, b) => Encode(a) == Encode(b), v => Encode(v).GetHashCode(), v => Decode<T>(Encode(v))));
    }

    private void EnforceTenant()
    {
        foreach (var entry in ChangeTracker.Entries<IMustHaveTenant>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.TenantId)) entry.Entity.TenantId = TenantId;
            if (entry.Entity.TenantId != TenantId ||
                (entry.State != EntityState.Added && entry.Property(nameof(IMustHaveTenant.TenantId)).OriginalValue as string != TenantId))
                throw new ApiException(403, "Không được ghi dữ liệu của studio khác.", "tenant_mismatch");
        }
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) { EnforceTenant(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    { EnforceTenant(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
}
