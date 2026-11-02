using System.Net;
using System.Net.Mail;
using System.Text;
using StudioCore.Models;

namespace StudioCore.Services;

public sealed record EmailDeliveryStatus(bool IsConfigured, bool IsTestMode, string Mode, string[] MissingOrInvalid);

public sealed class AccountEmailSender(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<AccountEmailSender> logger)
{
    public bool IsTestMode => string.Equals(configuration["Email:DeliveryMode"], "test", StringComparison.OrdinalIgnoreCase);
    public EmailDeliveryStatus Status => InspectConfiguration();
    public bool IsConfigured => Status.IsConfigured;
    public string PublicOrigin => (configuration["Saas:PublicOrigin"] ?? "").TrimEnd('/');

    public void LogConfigurationStatus()
    {
        var status = Status;
        if (status.IsConfigured)
            logger.LogInformation("Email delivery is ready in {Mode} mode.", status.Mode);
        else
            logger.LogInformation("Optional legacy email delivery is disabled. Missing/invalid settings: {Settings}.",
                string.Join(", ", status.MissingOrInvalid));
    }

    public async Task SendAsync(string recipient, string subject, string body)
    {
        if (IsTestMode) return;
        if (!IsConfigured)
            throw new ApiException(503, "Hệ thống chưa cấu hình gửi email.", "email_not_configured");

        try
        {
            using var smtp = new SmtpClient(configuration["Email:SmtpHost"], configuration.GetValue("Email:SmtpPort", 587))
            {
                EnableSsl = configuration.GetValue("Email:EnableSsl", true),
                Timeout = Math.Clamp(configuration.GetValue("Email:TimeoutSeconds", 20), 5, 120) * 1000
            };
            var username = configuration["Email:Username"]?.Trim();
            if (!string.IsNullOrWhiteSpace(username))
                smtp.Credentials = new NetworkCredential(username, configuration["Email:Password"]);

            var from = new MailAddress(configuration["Email:From"]!,
                configuration["Email:FromDisplayName"]?.Trim() ?? "StudioCore", Encoding.UTF8);
            using var mail = new MailMessage(from, new MailAddress(recipient))
            {
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                Body = body,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = false
            };
            await smtp.SendMailAsync(mail);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or FormatException)
        {
            logger.LogWarning(exception, "SMTP delivery failed for recipient domain {Domain}.", RecipientDomain(recipient));
            throw new ApiException(503, "Chưa gửi được email. Vui lòng thử lại sau hoặc liên hệ hỗ trợ.", "email_failed");
        }
    }

    private EmailDeliveryStatus InspectConfiguration()
    {
        if (IsTestMode) return new EmailDeliveryStatus(true, true, "test", []);
        var invalid = new List<string>();
        var host = configuration["Email:SmtpHost"]?.Trim();
        var from = configuration["Email:From"]?.Trim();
        var username = configuration["Email:Username"]?.Trim();
        var password = configuration["Email:Password"];
        var port = configuration.GetValue("Email:SmtpPort", 587);
        if (string.IsNullOrWhiteSpace(host)) invalid.Add("Email__SmtpHost");
        if (port is < 1 or > 65535) invalid.Add("Email__SmtpPort");
        if (string.IsNullOrWhiteSpace(from) || !MailAddress.TryCreate(from, out _)) invalid.Add("Email__From");
        if (string.IsNullOrWhiteSpace(username) != string.IsNullOrWhiteSpace(password))
            invalid.Add("Email__Username/Email__Password");
        if (!Uri.TryCreate(PublicOrigin, UriKind.Absolute, out var origin) ||
            (!environment.IsDevelopment() && origin.Scheme != Uri.UriSchemeHttps))
            invalid.Add("Saas__PublicOrigin");
        return new EmailDeliveryStatus(invalid.Count == 0, false, "smtp", invalid.ToArray());
    }

    private static string RecipientDomain(string recipient)
    {
        var separator = recipient.LastIndexOf('@');
        return separator >= 0 && separator + 1 < recipient.Length ? recipient[(separator + 1)..] : "invalid";
    }
}
