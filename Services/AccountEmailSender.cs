using System.Net;
using System.Net.Mail;
using StudioCore.Models;

namespace StudioCore.Services;

public sealed class AccountEmailSender(IConfiguration configuration)
{
    public bool IsTestMode => string.Equals(configuration["Email:DeliveryMode"], "test", StringComparison.OrdinalIgnoreCase);
    public bool IsConfigured => IsTestMode ||
        (!string.IsNullOrWhiteSpace(configuration["Email:SmtpHost"]) &&
         !string.IsNullOrWhiteSpace(configuration["Email:From"]) &&
         !string.IsNullOrWhiteSpace(configuration["Saas:PublicOrigin"]));

    public string PublicOrigin => (configuration["Saas:PublicOrigin"] ?? "").TrimEnd('/');

    public async Task SendAsync(string recipient, string subject, string body)
    {
        if (IsTestMode) return;
        if (!IsConfigured) throw new InvalidOperationException("Email delivery is not configured.");
        try
        {
            using var smtp = new SmtpClient(configuration["Email:SmtpHost"], configuration.GetValue("Email:SmtpPort", 587))
            {
                EnableSsl = configuration.GetValue("Email:EnableSsl", true)
            };
            if (!string.IsNullOrWhiteSpace(configuration["Email:Username"]))
                smtp.Credentials = new NetworkCredential(configuration["Email:Username"], configuration["Email:Password"]);
            using var mail = new MailMessage(configuration["Email:From"]!, recipient, subject, body);
            await smtp.SendMailAsync(mail);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            throw new ApiException(503, "Chưa gửi được email. Vui lòng thử lại sau hoặc liên hệ hỗ trợ.", "email_failed");
        }
    }
}
