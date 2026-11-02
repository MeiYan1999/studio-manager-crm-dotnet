using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using StudioCore.Models;
using StudioCore.Persistence;
using StudioCore.Services;

namespace StudioCore.Api;

public static class GoogleAuthEndpoints
{
    public const string ExternalCookie = "StudioCore.GoogleExternal";
    public const string PlatformCookie = "StudioCore.PlatformIdentity";

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/auth/google/status", (IConfiguration configuration) => Results.Ok(new
        {
            Enabled = IsConfigured(configuration)
        }));

        app.MapGet("/api/auth/google/start", (HttpContext context, IConfiguration configuration) =>
        {
            RequirePlatform(context);
            if (!IsConfigured(configuration))
                throw new ApiException(503, "Đăng nhập Google chưa được cấu hình.", "google_not_configured");
            var properties = new AuthenticationProperties { RedirectUri = "/api/auth/google/complete" };
            properties.SetParameter("prompt", "select_account");
            return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
        }).RequireRateLimiting("onboarding");

        app.MapGet("/api/auth/google/complete", async (HttpContext context, GoogleLoginService loginService,
            AdminSessionStore sessions) =>
        {
            RequirePlatform(context);
            var external = await context.AuthenticateAsync(ExternalCookie);
            if (!external.Succeeded || external.Principal is null)
                return Results.Redirect("/?authError=google_failed#bat-dau");

            var subject = external.Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var email = external.Principal.FindFirstValue(ClaimTypes.Email) ?? "";
            var name = external.Principal.FindFirstValue(ClaimTypes.Name) ?? email.Split('@')[0];
            var verified = external.Principal.FindFirstValue("google_email_verified");
            if (!string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase))
            {
                await context.SignOutAsync(ExternalCookie);
                return Results.Redirect("/?authError=email_unverified#bat-dau");
            }

            try
            {
                var result = loginService.SignInOrProvision(subject, email, name);
                await context.SignInAsync(PlatformCookie, new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, result.GoogleSubject),
                    new Claim(ClaimTypes.Email, email),
                    new Claim(ClaimTypes.Name, name)
                ], PlatformCookie)), new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) });
                await context.SignOutAsync(ExternalCookie);

                if (result.Workspaces.Count != 1) return Results.Redirect("/workspaces");
                var workspace = loginService.SelectWorkspace(subject, result.Workspaces[0].Studio.Id);
                TenantContext.Set(context, workspace.Studio.Id);
                sessions.SignIn(context, workspace.User);
                var welcome = result.IsNewStudio ? "?welcome=1" : "";
                return Results.Redirect($"/s/{Uri.EscapeDataString(workspace.Studio.Slug ?? "")}/admin{welcome}");
            }
            catch (ApiException exception)
            {
                await context.SignOutAsync(ExternalCookie);
                var code = Uri.EscapeDataString(exception.Code ?? "google_failed");
                return Results.Redirect($"/?authError={code}#bat-dau");
            }
        }).RequireRateLimiting("onboarding");

        app.MapGet("/api/auth/workspaces", async (HttpContext context, GoogleLoginService loginService) =>
        {
            RequirePlatform(context);
            var subject = await RequireIdentity(context);
            return Results.Ok(new { Workspaces = loginService.ListWorkspaces(subject) });
        });

        app.MapPost("/api/auth/workspaces/select", async (SelectWorkspaceRequest request, HttpContext context,
            GoogleLoginService loginService, AdminSessionStore sessions) =>
        {
            RequirePlatform(context);
            var subject = await RequireIdentity(context);
            if (string.IsNullOrWhiteSpace(request.TenantId))
                throw new ApiException(400, "Chưa chọn studio.", "workspace_required");
            var workspace = loginService.SelectWorkspace(subject, request.TenantId);
            TenantContext.Set(context, workspace.Studio.Id);
            sessions.SignIn(context, workspace.User);
            return Results.Ok(new { RedirectUrl = $"/s/{Uri.EscapeDataString(workspace.Studio.Slug ?? "")}/admin" });
        });

        app.MapPost("/api/auth/google/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(PlatformCookie);
            return Results.NoContent();
        });
    }

    private static async Task<string> RequireIdentity(HttpContext context)
    {
        var identity = await context.AuthenticateAsync(PlatformCookie);
        var subject = identity.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!identity.Succeeded || string.IsNullOrWhiteSpace(subject))
            throw new ApiException(401, "Vui lòng đăng nhập Google.", "google_auth_required");
        return subject;
    }

    private static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"]) &&
        !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]);

    private static void RequirePlatform(HttpContext context)
    {
        if (!context.Items.ContainsKey("StudioCore.PlatformHost"))
            throw new ApiException(404, "Đăng nhập Google chỉ bắt đầu từ StudioCore.", "not_found");
    }
}

public sealed record SelectWorkspaceRequest(string TenantId);
