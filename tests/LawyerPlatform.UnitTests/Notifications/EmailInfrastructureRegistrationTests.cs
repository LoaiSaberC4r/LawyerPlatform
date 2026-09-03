using BuildingBlock.Application.Email;
using BuildingBlock.Infrastructure.Options;
using LawyerPlatform.Application.Notifications.Email;
using LawyerPlatform.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.UnitTests.Notifications;

public sealed class EmailInfrastructureRegistrationTests
{
    [Fact]
    public void AddLawyerPlatformInfrastructure_BindsSmtpCredentialsAndResolvesEmailServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = "Server=(localdb)\\mssqllocaldb;Database=LawyerPlatformEmailRegistration;Trusted_Connection=True;",
                ["Smtp:Host"] = "smtp.example.test",
                ["Smtp:Port"] = "2525",
                ["Smtp:UseSsl"] = "false",
                ["Smtp:RequireStartTls"] = "true",
                ["Smtp:UserName"] = "configured-user",
                ["Smtp:Password"] = "configured-password",
                ["Smtp:FromEmail"] = "no-reply@example.test",
                ["Smtp:FromName"] = "Lawyer Platform",
                ["Smtp:MaxAttachmentBytes"] = "10485760",
                ["Smtp:MaxTotalAttachmentBytes"] = "26214400",
                ["Smtp:MaxRecipients"] = "100",
                ["Smtp:MaxAttachments"] = "20",
                ["Smtp:TimeoutMilliseconds"] = "100000",
                ["InitialSuperAdmin:Enabled"] = "false",
                ["PasswordLifecycle:ExpiryDays"] = "90",
                ["Authentication:Jwt:Issuer"] = "issuer",
                ["Authentication:Jwt:Audience"] = "audience",
                ["Authentication:Jwt:Key"] = new string('k', 64),
                ["Authentication:Jwt:AccessTokenExpirationMinutes"] = "60",
                ["LawyerDocuments:RequiredDocumentTypes:0"] = "IdentityVerification",
                ["LawyerDocuments:AllowedExtensions:0"] = ".pdf",
                ["LawyerDocuments:AllowedContentTypes:0"] = "application/pdf",
                ["LawyerDocuments:MaximumFileSizeBytes"] = "1048576",
                ["ConsultationScheduling:TimeZoneId"] = "Africa/Cairo",
                ["EmailOutbox:Enabled"] = "false",
                ["EmailOutbox:PollingIntervalSeconds"] = "15",
                ["EmailOutbox:BatchSize"] = "20",
                ["EmailOutbox:MaxAttempts"] = "5",
                ["EmailOutbox:ClaimLeaseSeconds"] = "300",
                ["ContactUs:SupportEmail"] = "support@example.test",
                ["EmailBranding:FooterImageUrl"] = "https://cdn.example.test/email-assets/avokatoo-email-footer.png",
                ["FrontendUrls:ConsultationTrackingUrl"] = "https://frontend.example.test/consultation/track"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(
            new TestHostEnvironment(Environments.Development));

        services.AddLawyerPlatformInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var smtp = scope.ServiceProvider.GetRequiredService<IOptions<SmtpOptions>>().Value;
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEmailSender>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEmailNotificationOutbox>());
        Assert.Equal(
            "https://cdn.example.test/email-assets/avokatoo-email-footer.png",
            scope.ServiceProvider.GetRequiredService<IEmailBrandingProvider>().FooterImageUrl);
        Assert.Equal(
            "https://frontend.example.test/consultation/track",
            scope.ServiceProvider.GetRequiredService<IFrontendUrlsProvider>().ConsultationTrackingUrl);
        Assert.Equal("smtp.example.test", smtp.Host);
        Assert.Equal(2525, smtp.Port);
        Assert.Equal("configured-user", smtp.UserName);
        Assert.Equal("configured-password", smtp.Password);
        Assert.Equal("no-reply@example.test", smtp.FromEmail);
        Assert.Equal("Lawyer Platform", smtp.FromName);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "LawyerPlatform.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
