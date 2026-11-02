using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using StudioCore.Models;
using StudioCore.Persistence;
using StudioCore.Services;
using Xunit;

namespace StudioCore.Tests;

public sealed class SaasTests : IDisposable
{
    readonly string directory = Path.Combine(Path.GetTempPath(), "studiocore-saas-" + Guid.NewGuid().ToString("N"));
    readonly HttpContextAccessor accessor = new() { HttpContext = new DefaultHttpContext() };
    readonly SaasDatabase database;
    readonly IConfiguration configuration;
    public SaasTests()
    {
        configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Data:Directory"] = directory, ["Billing:BankCode"] = "TEST", ["Billing:AccountNumber"] = "test-account",
            ["SePay:ApiKey"] = "test-only-webhook-key"
        }).Build();
        database = new SaasDatabase(new TestEnvironment { ContentRootPath = directory }, configuration, new TenantContext(accessor));
        database.Initialize(new AppData { Settings = new StudioSettings { StudioName = "Original studio" },
            Users = [new StudioUser { Id = "owner", Username = "owner", Role = "admin" }],
            Customers = [new Customer { Id = "same-id", Name = "Master customer" }] });
        using var db = database.Open();
        db.Studios.Add(new Studio { Id = "tenant-b", Slug = "studio-b" }); db.SaveChanges();
        using var b = database.Open("tenant-b");
        b.Workspaces.Add(new TenantWorkspace { MetadataJson = JsonSerializer.Serialize(new AppData(), new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        b.Add(new Customer { Id = "same-id", Name = "B customer" }); b.SaveChanges();
    }
    [Fact] public void TenantFiltersAndLegacyImportKeepIdsAndRowsIsolated()
    {
        Assert.Equal("Master customer", database.Read(d => Assert.Single(d.Customers).Name));
        TenantContext.Set(accessor.HttpContext!, "tenant-b");
        Assert.Equal("B customer", database.Read(d => Assert.Single(d.Customers).Name));
        Assert.Equal("tenant-b", database.Read(d => d.Customers[0].TenantId));
        using var db = database.Open();
        Assert.Equal(2, db.Set<Customer>().IgnoreQueryFilters().Count());
    }
    [Fact] public void SystemTenantSeedAttachesRootDomainAndIsIdempotent()
    {
        var legacy = new AppData { Settings = new StudioSettings { StudioName = "Ignored second import" } };
        database.Initialize(legacy, "https://www.studiocore.io.vn/");
        database.Initialize(legacy, "studiocore.io.vn");
        using var db = database.Open(Studio.MasterId);
        var master = db.Studios.Single(studio => studio.Id == Studio.MasterId);
        Assert.Equal("studiocore.io.vn", master.CustomDomain);
        Assert.True(master.CustomDomainVerified);
        Assert.Single(db.Workspaces);
        Assert.Equal("Original studio", database.Read(data => data.Settings.StudioName));
    }
    [Fact] public void GoogleLoginCreatesOneFreeTenantAndReusesImmutableSubject()
    {
        var service = new GoogleLoginService(database, new StudioProvisioning(database));
        var first = service.SignInOrProvision("google-subject-1", "owner@example.com", "Ánh Dương");
        var second = service.SignInOrProvision("google-subject-1", "changed@example.com", "Tên mới");

        Assert.True(first.IsNewStudio);
        Assert.False(second.IsNewStudio);
        Assert.Equal(Assert.Single(first.Workspaces).Studio.Id, Assert.Single(second.Workspaces).Studio.Id);
        Assert.Equal(SubscriptionPlan.Free, Assert.Single(first.Workspaces).Studio.CurrentPlan);
        Assert.Equal("google-subject-1", Assert.Single(second.Workspaces).User.GoogleSubject);
        Assert.True(Assert.Single(second.Workspaces).User.IsEmailVerified);
        Assert.Equal("anh-duong", Assert.Single(first.Workspaces).Studio.Slug);
    }

    [Fact] public void GoogleLoginLinksExactlyOnePreviouslyVerifiedEmail()
    {
        TenantContext.Set(accessor.HttpContext!, "tenant-b");
        database.Write(data =>
        {
            data.Users.Add(new StudioUser { Id = "existing-google-owner", Username = "existing", Email = "existing@example.com", IsEmailVerified = true, Role = "admin" });
            return 0;
        });
        var service = new GoogleLoginService(database, new StudioProvisioning(database));
        var result = service.SignInOrProvision("google-subject-existing", "existing@example.com", "Existing Owner");

        Assert.False(result.IsNewStudio);
        Assert.Equal("tenant-b", Assert.Single(result.Workspaces).Studio.Id);
        Assert.Equal("google-subject-existing", Assert.Single(result.Workspaces).User.GoogleSubject);
    }
    [Fact] public void GoogleLoginDoesNotClaimAmbiguousLegacyEmail()
    {
        database.Mutate(Studio.MasterId, (db, _) =>
        {
            db.Add(new StudioUser { Id = "legacy-master", Username = "legacy-master", Email = "shared@example.com", IsEmailVerified = true, Role = "admin" });
            return 0;
        });
        database.Mutate("tenant-b", (db, _) =>
        {
            db.Add(new StudioUser { Id = "legacy-tenant", Username = "legacy-tenant", Email = "shared@example.com", IsEmailVerified = true, Role = "admin" });
            return 0;
        });

        var service = new GoogleLoginService(database, new StudioProvisioning(database));
        var result = service.SignInOrProvision("google-shared", "shared@example.com", "Shared User");

        Assert.True(result.IsNewStudio);
        Assert.DoesNotContain(result.Workspaces, workspace => workspace.Studio.Id is Studio.MasterId or "tenant-b");
        using var lookup = database.Open(Studio.MasterId);
        Assert.Equal(2, lookup.Set<StudioUser>().IgnoreQueryFilters().Count(user => user.Email == "shared@example.com" && user.GoogleSubject == null));
    }

    [Fact] public void DisabledGoogleIdentityCannotListOrSelectAnyWorkspace()
    {
        var service = new GoogleLoginService(database, new StudioProvisioning(database));
        var login = service.SignInOrProvision("google-disabled", "disabled@example.com", "Disabled User");
        var workspace = Assert.Single(login.Workspaces);
        using (var lookup = database.Open(Studio.MasterId))
        {
            var identity = lookup.GoogleIdentities.Single(candidate => candidate.GoogleSubject == "google-disabled");
            identity.IsActive = false;
            lookup.SaveChanges();
        }

        Assert.Equal("account_disabled", Assert.Throws<ApiException>(() => service.ListWorkspaces("google-disabled")).Code);
        Assert.Equal("account_disabled", Assert.Throws<ApiException>(() => service.SelectWorkspace("google-disabled", workspace.Studio.Id)).Code);
    }

    [Fact] public void PendingInvitationKeyIsUniqueWithinTenantButReusableAfterCancellation()
    {
        database.Mutate("tenant-b", (db, _) =>
        {
            db.Add(new StudioInvitation { Email = "unique@example.com", PendingKey = "unique@example.com", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
            return 0;
        });
        Assert.ThrowsAny<Exception>(() => database.Mutate("tenant-b", (db, _) =>
        {
            db.Add(new StudioInvitation { Email = "unique@example.com", PendingKey = "unique@example.com", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
            return 0;
        }));
        database.Mutate("tenant-b", (db, _) =>
        {
            var invitation = db.Set<StudioInvitation>().Single(candidate => candidate.PendingKey == "unique@example.com");
            invitation.CancelledAt = DateTimeOffset.UtcNow;
            invitation.PendingKey = null;
            return 0;
        });
        database.Mutate("tenant-b", (db, _) =>
        {
            db.Add(new StudioInvitation { Email = "unique@example.com", PendingKey = "unique@example.com", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
            return 0;
        });
    }
    [Fact] public void GoogleLoginAcceptsPreapprovedInvitationsAcrossMultipleStudiosWithoutCreatingFreeTenant()
    {
        database.Mutate("tenant-b", (db, studio) =>
        {
            studio.CurrentPlan = SubscriptionPlan.ErpStudio; studio.MaxUsers = 5;
            db.Add(new StudioInvitation { Email = "staff@example.com", Role = "sale", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
            return 0;
        });
        var service = new GoogleLoginService(database, new StudioProvisioning(database));
        var first = service.SignInOrProvision("google-staff", "staff@example.com", "Studio Staff");
        Assert.False(first.IsNewStudio);
        Assert.Equal("tenant-b", Assert.Single(first.Workspaces).Studio.Id);
        Assert.Equal("sale", Assert.Single(first.Workspaces).User.Role);

        using (var root = database.Open(Studio.MasterId))
        {
            root.Studios.Add(new Studio { Id = "tenant-c", Slug = "studio-c", CurrentPlan = SubscriptionPlan.ErpStudio, MaxUsers = 5 });
            root.SaveChanges();
        }
        using (var tenant = database.Open("tenant-c"))
        {
            tenant.Workspaces.Add(new TenantWorkspace { MetadataJson = JsonSerializer.Serialize(new AppData(), new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
            tenant.Add(new StudioInvitation { Email = "staff@example.com", Role = "employee", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
            tenant.SaveChanges();
        }
        var second = service.SignInOrProvision("google-staff", "staff@example.com", "Studio Staff");
        Assert.Equal(2, second.Workspaces.Count);
        Assert.Equal(2, second.Workspaces.Select(workspace => workspace.User.TenantId).Distinct().Count());
    }
    [Fact] public void PendingInvitationWithoutAvailablePlanDoesNotCreateAnUnrelatedFreeStudio()
    {
        database.Mutate("tenant-b", (db, studio) =>
        {
            studio.CurrentPlan = SubscriptionPlan.Free;
            db.Add(new StudioInvitation { Email = "blocked@example.com", Role = "employee", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
            return 0;
        });
        using var before = database.Open(Studio.MasterId);
        var studioCount = before.Studios.Count();
        var service = new GoogleLoginService(database, new StudioProvisioning(database));
        var exception = Assert.Throws<ApiException>(() => service.SignInOrProvision("google-blocked", "blocked@example.com", "Blocked Staff"));
        Assert.Equal("invitation_pending_unavailable", exception.Code);
        using var after = database.Open(Studio.MasterId);
        Assert.Equal(studioCount, after.Studios.Count());
    }
    [Fact] public void FailingWriterRollsBackAndCrossTenantPayloadIsRejected()
    {
        TenantContext.Set(accessor.HttpContext!, "tenant-b");
        Assert.Throws<InvalidOperationException>(() => database.Write<int>(d => { d.Customers.Clear(); throw new InvalidOperationException(); }));
        Assert.Single(database.Read(d => d.Customers));
        Assert.Throws<ApiException>(() => database.Write(d => { d.Customers.Add(new Customer { TenantId = Studio.MasterId }); return 0; }));
        Assert.Single(database.Read(d => d.Customers));
    }
    [Fact] public async Task SaveChangesAsyncRejectsAnotherTenantsEntity()
    {
        using var db = database.Open("tenant-b");
        db.Add(new Customer { TenantId = Studio.MasterId });
        await Assert.ThrowsAsync<ApiException>(() => db.SaveChangesAsync());
    }
    [Fact] public void UserSlotsAreAtomicAndCannotBeBypassedByDirectWrites()
    {
        TenantContext.Set(accessor.HttpContext!, "tenant-b");
        database.Write(d => { d.Users.Add(new StudioUser { Username = "first" }); return 0; });
        Assert.Throws<ApiException>(() => database.Write(d => { d.Users.Add(new StudioUser { Username = "second" }); return 0; }));
        Assert.Single(database.Read(d => d.Users));
    }
    [Fact] public void JsonChildrenAndScalarUpdatesSurviveDatabaseReload()
    {
        database.Write(d => { d.Albums.Add(new Album { Id = "album", ShareToken = "unique", Photos = [new Photo { Code = "SAM0001" }] }); return 0; });
        database.Write(d => { d.Albums[0].Selections.Add(new PhotoSelection { PhotoId = d.Albums[0].Photos[0].Id }); d.Customers[0].Name = "Changed"; return 0; });
        Assert.Single(database.Read(d => d.Albums[0].Selections));
        Assert.Equal("Changed", database.Read(d => d.Customers[0].Name));
    }
    [Fact] public void CollectionOrderSurvivesImportAndUpdatesRegardlessOfDatabaseKeyOrder()
    {
        database.Write(d => { d.PriceCatalog = [new PriceGroup { Id = "z-first" }, new PriceGroup { Id = "a-second" }]; return 0; });
        Assert.Equal(new[] { "z-first", "a-second" }, database.Read(d => d.PriceCatalog.Select(g => g.Id).ToArray()));
        database.Write(d => { d.PriceCatalog.Reverse(); return 0; });
        Assert.Equal(new[] { "a-second", "z-first" }, database.Read(d => d.PriceCatalog.Select(g => g.Id).ToArray()));
    }
    [Fact] public void MonthResetUsesVietnamTimeAndExpiredPlansLosePaidFeatures()
    {
        var s = new Studio { CurrentPlan = SubscriptionPlan.ErpStudio, SubscriptionExpiresAt = DateTimeOffset.Parse("2026-10-01T00:00:00Z"), PcFilterMonth = "2026-09", CurrentMonthPcFilterUsed = 5 };
        PlanRules.Refresh(s, DateTimeOffset.Parse("2026-09-30T17:00:00Z"));
        Assert.Equal(0, s.CurrentMonthPcFilterUsed); Assert.Equal("2026-10", s.PcFilterMonth);
        PlanRules.Refresh(s, DateTimeOffset.Parse("2026-10-02T00:00:00Z"));
        Assert.Equal(SubscriptionPlan.ErpStudio, s.CurrentPlan); Assert.Equal(SubscriptionPlan.Free, PlanRules.Effective(s, DateTimeOffset.Parse("2026-10-02T00:00:00Z"))); Assert.Equal(1, s.MaxUsers); Assert.Equal(5, s.MonthlyPcFilterQuota);
    }
    [Theory]
    [InlineData(SubscriptionPlan.AlbumPro, 75000)] [InlineData(SubscriptionPlan.ErpSolo, 125000)] [InlineData(SubscriptionPlan.ErpStudio, 225000)]
    public void PlanPricesUseServerAmountsAndTwelveMonthsForAnnual(SubscriptionPlan plan, int monthly)
    { Assert.Equal(monthly, PlanRules.Price(plan, BillingCycle.Monthly)); Assert.Equal(monthly * 12, PlanRules.Price(plan, BillingCycle.Yearly)); }
    [Fact] public void CheckoutDoesNotReusePendingTransactionWithDifferentPrice()
    {
        using (var db = database.Open("tenant-b"))
        {
            db.SubscriptionTransactions.Add(new SubscriptionTransaction { Plan = SubscriptionPlan.AlbumPro, BillingCycle = BillingCycle.Monthly, Amount = 1, PaymentCode = "SCOLDPRICE12345678" });
            db.SaveChanges();
        }
        var billing = new SubscriptionBilling(database, configuration, new BillingEvents());
        var checkout = JsonSerializer.SerializeToElement(billing.Checkout("tenant-b", SubscriptionPlan.AlbumPro, BillingCycle.Monthly), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(75000, checkout.GetProperty("amount").GetDecimal());
        Assert.NotEqual("SCOLDPRICE12345678", checkout.GetProperty("paymentCode").GetString());
        Assert.Contains("amount=75000", checkout.GetProperty("qrUrl").GetString());
    }
    [Fact] public void PaymentChecksAmountAndReplayCannotExtendSubscriptionTwice()
    {
        var billing = new SubscriptionBilling(database, configuration, new BillingEvents());
        var checkout = JsonSerializer.SerializeToElement(billing.Checkout("tenant-b", SubscriptionPlan.AlbumPro, BillingCycle.Monthly), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var message = new SePayNotification { Id = 123, AccountNumber = "test-account", TransferType = "in", Content = checkout.GetProperty("description").GetString()!, TransferAmount = 1 };
        Assert.Throws<ApiException>(() => billing.Process(message));
        message.TransferAmount = 75000; billing.Process(message);
        using var db = database.Open("tenant-b");
        var end = db.Studios.AsNoTracking().Single(s => s.Id == "tenant-b").SubscriptionExpiresAt;
        billing.Process(message);
        Assert.Equal(end, db.Studios.AsNoTracking().Single(s => s.Id == "tenant-b").SubscriptionExpiresAt);
        Assert.Equal(SubscriptionPaymentStatus.Paid, db.SubscriptionTransactions.Single().Status);
    }
    [Fact] public void InvalidWebhookAuthenticationIsRejected()
    {
        var billing = new SubscriptionBilling(database, configuration, new BillingEvents());
        Assert.Throws<ApiException>(() => billing.Verify(new DefaultHttpContext().Request, []));
    }
    [Fact] public void HmacRequiresFreshTimestampAndExactRawBodyEvenWithApiKeyConfigured()
    {
        var config = new ConfigurationBuilder().AddConfiguration(configuration).AddInMemoryCollection(new Dictionary<string, string?> {
            ["SePay:WebhookSecret"] = "test-only-hmac-secret" }).Build();
        var billing = new SubscriptionBilling(database, config, new BillingEvents());
        var request = new DefaultHttpContext().Request;
        var body = Encoding.UTF8.GetBytes("{\"id\":1}");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        request.Headers["X-SePay-Timestamp"] = timestamp;
        request.Headers["X-SePay-Signature"] = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("test-only-hmac-secret"), Encoding.UTF8.GetBytes(timestamp + ".").Concat(body).ToArray())).ToLowerInvariant();
        billing.Verify(request, body);
        Assert.Throws<ApiException>(() => billing.Verify(request, Encoding.UTF8.GetBytes("{\"id\":2}")));
        request.Headers["X-SePay-Timestamp"] = "1";
        Assert.Throws<ApiException>(() => billing.Verify(request, body));
        request.Headers.Clear(); request.Headers.Authorization = "Apikey test-only-webhook-key";
        Assert.Throws<ApiException>(() => billing.Verify(request, body));
    }
    [Theory]
    [InlineData("platform.test", "/s/studio-b/api/admin/albums", "tenant-b", "/api/admin/albums")]
    [InlineData("studio-b.platform.test", "/api/admin/albums", "tenant-b", "/api/admin/albums")]
    [InlineData("gallery.client.test", "/api/admin/albums", "tenant-b", "/api/admin/albums")]
    public async Task ResolverSupportsThreeModes(string host, string path, string tenantId, string rewritten)
    {
        database.Mutate("tenant-b", (_, studio) => { studio.CurrentPlan = SubscriptionPlan.AlbumPro; studio.CustomDomain = "gallery.client.test"; studio.CustomDomainVerified = true; return 0; });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Saas:BaseDomain"] = "platform.test" }).Build();
        var context = new DefaultHttpContext(); context.Request.Host = new HostString(host); context.Request.Path = path;
        var called = false;
        var middleware = new TenantResolverMiddleware(c => { called = true; Assert.Equal(tenantId, new TenantContext(new HttpContextAccessor { HttpContext = c }).TenantId); Assert.Equal(rewritten, c.Request.Path.Value); return Task.CompletedTask; });
        await middleware.InvokeAsync(context, database, config); Assert.True(called);
    }
    [Fact] public async Task ResolverRejectsUnverifiedExpiredOrCrossStudioPaths()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Saas:BaseDomain"] = "platform.test" }).Build();
        var middleware = new TenantResolverMiddleware(_ => throw new Xunit.Sdk.XunitException("Unresolved request reached application"));
        database.Mutate("tenant-b", (_, studio) => { studio.CurrentPlan = SubscriptionPlan.AlbumPro; studio.CustomDomain = "gallery.client.test"; return 0; });
        var context = new DefaultHttpContext(); context.Request.Host = new HostString("gallery.client.test");
        await Assert.ThrowsAsync<ApiException>(() => middleware.InvokeAsync(context, database, config));
        database.Mutate("tenant-b", (_, studio) => { studio.CustomDomainVerified = true; studio.SubscriptionExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1); return 0; });
        await Assert.ThrowsAsync<ApiException>(() => middleware.InvokeAsync(context, database, config));
        context.Request.Host = new HostString("another.platform.test"); context.Request.Path = "/s/studio-b/api/admin/albums";
        await Assert.ThrowsAsync<ApiException>(() => middleware.InvokeAsync(context, database, config));
    }
    [Fact] public void PostgresMigrationScriptUsesProviderTypesAndPreservesLegacyTable()
    {
        using var db = new StudioDbContextFactory().CreateDbContext(["--postgres"]);
        var script = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().GenerateScript();
        Assert.Contains("timestamp with time zone", script);
        Assert.Contains("boolean", script);
        Assert.DoesNotContain("DROP TABLE app_state", script);
    }
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "StudioCore"; public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = ""; public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
