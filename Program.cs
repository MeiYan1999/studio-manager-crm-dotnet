using System.IO.Compression;
using System.Text.Json;
using StudioCore.Api;
using StudioCore.Models;
using StudioCore.Services;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<IObjectStorage, ObjectStorage>();
builder.Services.AddSingleton<AdminSessionStore>();
builder.Services.AddSingleton<GoogleDriveService>();
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
        "img-src 'self' data: blob: https://drive.google.com https://*.googleusercontent.com; " +
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
            new ApiErrorDto(exception.Message, exception.Code),
            context.RequestAborted);
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

async Task ServeAppShell(HttpContext context)
{
    var settings = app.Services.GetRequiredService<DataStore>().Read(data => new
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
        if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers.CacheControl = "no-cache";
        }
        else if (path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers.CacheControl = "public,max-age=604800";
        }
    }
});

AuthEndpoints.Map(app);
PublicEndpoints.Map(app);
AdminEndpoints.Map(app);

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    app = "StudioCore",
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

var store = app.Services.GetRequiredService<DataStore>();
app.Logger.LogInformation("Dữ liệu studio: {Path}", store.StorePath);
if (store.AdminCode == "studio2026")
{
    app.Logger.LogWarning(
        "Tài khoản quản trị mặc định là “admin” với mật khẩu studio2026. Hãy đổi mật khẩu sau khi đăng nhập lần đầu.");
}

app.Run();
