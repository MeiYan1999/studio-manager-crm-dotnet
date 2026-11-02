using System.IO.Compression;
using System.Text.Json;
using StudioCore.Api;
using StudioCore.Persistence;
using StudioCore.Models;
using StudioCore.Services;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddHttpContextAccessor();
var authKeyDirectory = Path.GetFullPath(Path.Combine(builder.Configuration["Data:Directory"] ?? "data", "auth-keys"), builder.Environment.ContentRootPath);
Directory.CreateDirectory(authKeyDirectory);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(authKeyDirectory))
    .SetApplicationName("StudioCore");
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddAuthentication()
    .AddCookie(GoogleAuthEndpoints.PlatformCookie, options =>
    {
        options.Cookie.Name = "studiocore_identity";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    })
    .AddCookie(GoogleAuthEndpoints.ExternalCookie, options =>
    {
        options.Cookie.Name = "studiocore_google_external";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.SignInScheme = GoogleAuthEndpoints.ExternalCookie;
        options.ClientId = string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientId"])
            ? "not-configured" : builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientSecret"])
            ? "not-configured" : builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.CallbackPath = "/signin-google";
        options.SaveTokens = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Events.OnCreatingTicket = context =>
        {
            // Google has returned both names across its OAuth user-info and
            // OpenID Connect payloads. Preserve one normalized claim so the
            // callback can enforce verified email without depending on the
            // exact upstream JSON shape.
            if (context.User.TryGetProperty("email_verified", out var verified) ||
                context.User.TryGetProperty("verified_email", out verified))
            {
                var isVerified = verified.ValueKind == System.Text.Json.JsonValueKind.True ||
                    (verified.ValueKind == System.Text.Json.JsonValueKind.String &&
                     bool.TryParse(verified.GetString(), out var parsed) && parsed);
                context.Identity?.AddClaim(new System.Security.Claims.Claim(
                    "google_email_verified",
                    isVerified ? "true" : "false"));
            }
            return Task.CompletedTask;
        };
    });
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("onboarding", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
    options.AddPolicy("account-lifecycle", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
});
builder.Services.AddSingleton<TenantContext>();
builder.Services.AddSingleton<SaasDatabase>();
builder.Services.AddSingleton<StudioProvisioning>();
builder.Services.AddSingleton<GoogleLoginService>();
builder.Services.AddSingleton<AccountEmailSender>();
builder.Services.AddSingleton<BillingEvents>();
builder.Services.AddSingleton<SubscriptionBilling>();
builder.Services.AddSingleton<ISubscriptionGuard, SubscriptionGuard>();
builder.Services.AddHostedService<SubscriptionMaintenance>();
builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<CloudflareStorageService>();
builder.Services.AddSingleton<LocalStorageService>();
builder.Services.AddSingleton<IStorageService>(services =>
    string.IsNullOrWhiteSpace(services.GetRequiredService<IConfiguration>()["CloudflareR2:ServiceUrl"])
        ? services.GetRequiredService<LocalStorageService>()
        : services.GetRequiredService<CloudflareStorageService>());
builder.Services.AddSingleton<AdminSessionStore>();
builder.Services.AddSingleton<GoogleDriveService>();
builder.Services.AddHttpClient("dns", client => { client.Timeout = TimeSpan.FromSeconds(12); client.DefaultRequestHeaders.Accept.ParseAdd("application/dns-json"); });
builder.Services.AddHttpClient("drive", client =>
{
    client.Timeout = TimeSpan.FromSeconds(50);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("StudioCore/1.0");
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
    [
        "application/json",
        "image/svg+xml"
    ]);
});

var app = builder.Build();
app.Services.GetRequiredService<AccountEmailSender>().LogConfigurationStatus();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseResponseCompression();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; " +
        "img-src 'self' data: blob: https://drive.google.com https://*.googleusercontent.com https://qr.sepay.vn; " +
        "style-src 'self' 'unsafe-inline'; " +
        "script-src 'self'; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'self'; " +
        "object-src 'none'");

    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store";
    }

    await next();
});

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        // The visitor closed the tab; there is no response to write.
    }
    catch (ApiException exception) when (!context.Response.HasStarted)
    {
        context.Response.Clear();
        context.Response.StatusCode = exception.StatusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(
            new ApiErrorDto(exception.Message, exception.Code, exception.Feature, exception.RequiredPlan),
            context.RequestAborted);
    }
    catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new ApiErrorDto("Dữ liệu vừa được thay đổi. Vui lòng tải lại và thử lại.", "concurrent_update"));
    }
    catch (Microsoft.EntityFrameworkCore.DbUpdateException) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new ApiErrorDto("Dữ liệu trùng mã hoặc không hợp lệ. Vui lòng kiểm tra và thử lại.", "data_conflict"));
    }
    catch (Exception exception) when (!context.Response.HasStarted)
    {
        app.Logger.LogError(exception, "Unhandled error for {Method} {Path}", context.Request.Method, context.Request.Path);
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(
            new ApiErrorDto("Có lỗi xảy ra phía máy chủ. Vui lòng thử lại.", "server_error"),
            context.RequestAborted);
    }
});

app.UseMiddleware<TenantResolverMiddleware>();
app.Use(async (context, next) =>
{
    // Tenant path routing rewrites /s/{slug}/api/... to /api/... inside the pipeline.
    // Set this after tenant resolution so authenticated and tenant-scoped responses
    // can never be stored by a browser, CDN, or intermediary proxy.
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "private,no-store,max-age=0";
        context.Response.Headers.Pragma = "no-cache";
    }

    await next();
});
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && context.Request.Path != "/api/payments/sepay-webhook" &&
        !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) &&
        context.Request.Headers.TryGetValue("Origin", out var origin) &&
        (!Uri.TryCreate(origin.ToString(), UriKind.Absolute, out var uri) || !string.Equals(uri.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase)))
        throw new ApiException(403, "Nguồn yêu cầu không hợp lệ.", "invalid_origin");
    await next(context);
});

async Task ServeAppShell(HttpContext context)
{
    var settings = app.Services.GetRequiredService<DataStore>().ReadMetadata(data => new
    {
        Name = string.IsNullOrWhiteSpace(data.Settings.Website.LoadingName)
            ? data.Settings.Website.BrandName : data.Settings.Website.LoadingName,
        Text = data.Settings.Website.LoadingText
    });
    var html = await File.ReadAllTextAsync(Path.Combine(app.Environment.WebRootPath!, "index.html"), context.RequestAborted);
    html = html.Replace("data-loading-name><span>", "data-loading-name>" + System.Net.WebUtility.HtmlEncode(settings.Name) + "<span>")
        .Replace("<p data-loading-text></p>", "<p data-loading-text>" + System.Net.WebUtility.HtmlEncode(settings.Text) + "</p>");
    context.Response.ContentType = "text/html; charset=utf-8";
    context.Response.Headers.CacheControl = "no-store";
    await context.Response.WriteAsync(html, context.RequestAborted);
}

app.Use(async (context, next) =>
{
    if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)) &&
        (context.Request.Path == "/" || context.Request.Path == "/index.html"))
    {
        await ServeAppShell(context);
        return;
    }
    await next(context);
});
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var path = context.Context.Request.Path.Value ?? "";
        if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers.CacheControl = "no-cache";
        }
        else if (path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers.CacheControl = "public,max-age=300,stale-while-revalidate=86400";
        }
        else if (path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers.CacheControl = "public,max-age=604800";
        }
    }
});

InvitationEndpoints.Map(app);
OnboardingEndpoints.Map(app);
GoogleAuthEndpoints.Map(app);
SaasEndpoints.Map(app);
AuthEndpoints.Map(app);
PublicEndpoints.Map(app);
AdminEndpoints.Map(app);

app.MapGet("/api/health", (AccountEmailSender emailSender) => Results.Ok(new
{
    status = "ok",
    app = "StudioCore",
    email = emailSender.IsConfigured ? "ready" : "not_configured",
    time = DateTimeOffset.UtcNow
}));

app.MapFallback(async context =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(
            new ApiErrorDto("Không tìm thấy API.", "not_found"),
            context.RequestAborted);
        return;
    }

    if (context.Request.Path.StartsWithSegments("/assets") || Path.HasExtension(context.Request.Path))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await ServeAppShell(context);
});

var saasDatabase = app.Services.GetRequiredService<SaasDatabase>();
await using (var startupDb = saasDatabase.Open(Studio.MasterId))
{
    // Apply pending PostgreSQL/SQLite migrations before any request can resolve a tenant.
    await startupDb.Database.MigrateAsync();
}
var store = app.Services.GetRequiredService<DataStore>();
var systemDomain = builder.Configuration["Saas:BaseDomain"] ?? "studiocore.io.vn";
store.InitializeSaas(systemDomain);
if (args.Contains("--create-studio"))
{
    var provisioned = app.Services.GetRequiredService<StudioProvisioning>().Create(
        builder.Configuration["Provisioning:Name"] ?? "", builder.Configuration["Provisioning:Slug"] ?? "",
        builder.Configuration["Provisioning:Username"] ?? "", builder.Configuration["Provisioning:Password"] ?? "",
        builder.Configuration["Provisioning:Email"] ?? "");
    var studio = provisioned.Studio;
    app.Logger.LogInformation("Studio created: {Code}, /s/{Slug}, plan Free", studio.StudioCode, studio.Slug);
    return;
}
app.Logger.LogInformation("Dữ liệu studio: {Path}", store.StorePath);
if (store.AdminCode == "studio2026")
{
    app.Logger.LogWarning(
        "Tài khoản quản trị mặc định là “admin” với mật khẩu studio2026. Hãy đổi mật khẩu sau khi đăng nhập lần đầu.");
}

app.Run();
