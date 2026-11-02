using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using StudioCore.Services;
using Xunit;

namespace StudioCore.Tests;

public sealed class EmailSenderTests
{
    [Fact]
    public void ProductionConfigurationRequiresSmtpFromCredentialsAndHttpsOrigin()
    {
        var sender = Create(new Dictionary<string, string?>
        {
            ["Email:SmtpHost"] = "smtp.example.test",
            ["Email:From"] = "hello@example.test",
            ["Email:Username"] = "smtp-user",
            ["Saas:PublicOrigin"] = "http://studiocore.io.vn"
        });

        Assert.False(sender.IsConfigured);
        Assert.Contains("Email__Username/Email__Password", sender.Status.MissingOrInvalid);
        Assert.Contains("Saas__PublicOrigin", sender.Status.MissingOrInvalid);
    }

    [Fact]
    public void CompleteProductionConfigurationIsReadyWithoutExposingSecrets()
    {
        var sender = Create(new Dictionary<string, string?>
        {
            ["Email:SmtpHost"] = "smtp.example.test",
            ["Email:SmtpPort"] = "587",
            ["Email:From"] = "hello@example.test",
            ["Email:Username"] = "smtp-user",
            ["Email:Password"] = "test-only-secret",
            ["Saas:PublicOrigin"] = "https://studiocore.io.vn"
        });

        Assert.True(sender.IsConfigured);
        Assert.Equal("smtp", sender.Status.Mode);
        Assert.Empty(sender.Status.MissingOrInvalid);
    }

    [Fact]
    public void TestDeliveryModeDoesNotRequireRealCredentials()
    {
        var sender = Create(new Dictionary<string, string?> { ["Email:DeliveryMode"] = "test" });
        Assert.True(sender.IsConfigured);
        Assert.True(sender.IsTestMode);
    }

    private static AccountEmailSender Create(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new AccountEmailSender(configuration,
            new TestEnvironment { EnvironmentName = "Production" },
            NullLogger<AccountEmailSender>.Instance);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "StudioCore";
        public string EnvironmentName { get; set; } = "Production";
        public string ContentRootPath { get; set; } = "";
        public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
