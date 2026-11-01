using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using StudioCore.Models;
using StudioCore.Services;

namespace StudioCore.Api;

public static class OnboardingEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/onboarding/register", async (RegistrationRequest request, HttpContext context, StudioProvisioning provisioning, AccountEmailSender emailSender) =>
        {
            if (!context.Items.ContainsKey("StudioCore.PlatformHost"))
                throw new ApiException(404, "Không tìm thấy trang đăng ký.", "not_found");
            var name = (request.StudioName ?? "").Trim();
            var email = (request.Email ?? "").Trim();
            var slug = (request.Slug ?? "").Trim().ToLowerInvariant();
            var username = (request.Username ?? "").Trim().ToLowerInvariant();
            var password = request.Password ?? "";
            if (name.Length is < 2 or > 60 || email.Length > 160 || !MailAddress.TryCreate(email, out var address) || address.Address != email || password.Length is < 8 or > 128)
                throw new ApiException(400, "Vui lòng kiểm tra tên studio, email và mật khẩu (tối thiểu 8 ký tự).", "invalid_registration");
            if (!emailSender.IsConfigured)
                throw new ApiException(503, "Hệ thống chưa cấu hình gửi email nên chưa thể tạo tài khoản mới.", "email_not_configured");
            try
            {
                var provisioned = provisioning.Create(name, slug, username, password, email, requireEmailVerification: true);
                var studio = provisioned.Studio;
                var verifyUrl = emailSender.PublicOrigin + $"/s/{studio.Slug}/admin/verify-email?token={provisioned.VerificationToken}";
                var emailSent = true;
                try
                {
                    await emailSender.SendAsync(email, "Xác thực tài khoản StudioCore", $"Chào mừng bạn đến StudioCore.\n\nXác thực email trong 24 giờ tại:\n{verifyUrl}\n\nNếu bạn không đăng ký, hãy bỏ qua email này.");
                }
                catch (ApiException exception) when (exception.Code == "email_failed")
                {
                    // Provisioning is already committed. Keep the studio recoverable through
                    // the resend endpoint instead of reporting the whole registration failed.
                    emailSent = false;
                }
                return Results.Created($"/s/{studio.Slug}/admin/login", new
                {
                    studio.Slug,
                    LoginUrl = $"/s/{studio.Slug}/admin/login",
                    VerificationRequired = true,
                    EmailSent = emailSent,
                    VerificationToken = emailSender.IsTestMode ? provisioned.VerificationToken : null
                });
            }
            catch (InvalidOperationException ex) { throw new ApiException(400, ex.Message, "invalid_registration"); }
            catch (DbUpdateException) { throw new ApiException(409, "Tên định danh đã được sử dụng. Vui lòng chọn tên khác.", "registration_conflict"); }
        }).RequireRateLimiting("onboarding");
    }
}

public sealed record RegistrationRequest(string? StudioName, string? Slug, string? Username, string? Email, string? Password);
