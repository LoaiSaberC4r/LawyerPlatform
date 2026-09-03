using BuildingBlock.Infrastructure.Bootstrap;
using BuildingBlock.Application.Email;
using LawyerPlatform.Application.Abstractions.Seeding;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;
using System.Data.Common;

namespace LawyerPlatform.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _webRootPath = Path.Combine(
        Path.GetTempPath(),
        "LawyerPlatformTests",
        Guid.NewGuid().ToString("N"),
        "wwwroot");

    public async Task SeedDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<IEnsureSeeding>()
            .SeedDatabaseAsync(cancellationToken);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var contentRootPath = builder.GetSetting(WebHostDefaults.ContentRootKey)
            ?? throw new InvalidOperationException("The test content root is unavailable.");
        Directory.CreateDirectory(_webRootPath);
        var sourceFooterImagePath = Path.Combine(
            contentRootPath,
            "wwwroot",
            "email-assets",
            "avokatoo-email-footer.png");
        var testFooterImagePath = Path.Combine(
            _webRootPath,
            "email-assets",
            "avokatoo-email-footer.png");
        Directory.CreateDirectory(Path.GetDirectoryName(testFooterImagePath)!);
        File.Copy(sourceFooterImagePath, testFooterImagePath, overwrite: true);

        builder.UseEnvironment("Development");
        builder.UseWebRoot(_webRootPath);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Jwt:Issuer"] = "LawyerPlatform.Api.Tests",
                ["Authentication:Jwt:Audience"] = "LawyerPlatform.TestClients",
                ["Authentication:Jwt:Key"] = new string('k', 64),
                ["Authentication:Jwt:AccessTokenExpirationMinutes"] = "60",
                ["InitialSuperAdmin:Enabled"] = "true",
                ["InitialSuperAdmin:Id"] = "11111111-1111-1111-1111-111111111111",
                ["InitialSuperAdmin:UserName"] = "superadmin",
                ["InitialSuperAdmin:Email"] = "admin@lawyerplatform.test",
                ["InitialSuperAdmin:PhoneNumber"] = "01000000000",
                ["InitialSuperAdmin:Password"] = "InitialPassword1",
                ["PasswordLifecycle:ExpiryDays"] = "90",
                ["PasswordReset:OtpLength"] = "6",
                ["PasswordReset:OtpExpirationMinutes"] = "5",
                ["PasswordReset:MaximumVerificationAttempts"] = "5",
                ["PasswordReset:ResendCooldownSeconds"] = "60",
                ["PasswordReset:ResetTokenExpirationMinutes"] = "10",
                ["PasswordReset:HmacSecret"] = "integration-test-password-reset-secret-0001",
                ["ConsultationScheduling:TimeZoneId"] = "Africa/Cairo",
                ["ContactUs:SupportEmail"] = "Support@avokatoo.com",
                ["DatabaseInitialization:ApplyMigrationsOnStartup"] = "false",
                ["DatabaseInitialization:ApplySeedingOnStartup"] = "false",
                ["FrontendUrls:ConsultationTrackingUrl"] = "https://frontend.example.test/consultation/track",
                ["EmailOutbox:Enabled"] = "false",
                ["Cors:AllowAnyOrigin"] = "false",
                ["Cors:AllowCredentials"] = "false",
                ["Cors:AllowedOrigins:0"] = "http://localhost:4200",
                ["Cors:AllowedOrigins:1"] = "https://localhost:4200",
                ["MediaStorage:RootPath"] = Path.Combine(_webRootPath, "uploads"),
                ["MediaStorage:MaxFileSizeBytes"] = "1048576",
                ["MediaStorage:AllowedExtensions:0"] = ".jpg",
                ["MediaStorage:AllowedExtensions:1"] = ".jpeg",
                ["MediaStorage:AllowedExtensions:2"] = ".png",
                ["MediaStorage:AllowedExtensions:3"] = ".pdf",
                ["MediaStorage:AllowedMimeTypes:0"] = "image/jpeg",
                ["MediaStorage:AllowedMimeTypes:1"] = "image/png",
                ["MediaStorage:AllowedMimeTypes:2"] = "application/pdf",
                ["ProfileImages:PublicPathBase"] = "/uploads",
                ["LawyerDocuments:RequiredDocumentTypes:0"] = "IdentityVerification",
                ["LawyerDocuments:RequiredDocumentTypes:1"] = "ProfessionalMembership",
                ["LawyerDocuments:AllowedExtensions:0"] = ".jpg",
                ["LawyerDocuments:AllowedExtensions:1"] = ".jpeg",
                ["LawyerDocuments:AllowedExtensions:2"] = ".png",
                ["LawyerDocuments:AllowedExtensions:3"] = ".pdf",
                ["LawyerDocuments:AllowedContentTypes:0"] = "image/jpeg",
                ["LawyerDocuments:AllowedContentTypes:1"] = "image/png",
                ["LawyerDocuments:AllowedContentTypes:2"] = "application/pdf",
                ["LawyerDocuments:MaximumFileSizeBytes"] = "1048576"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LawyerPlatformDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<LawyerPlatformDbContext>>();
            services.RemoveAll<LawyerPlatformDbContext>();
            services.RemoveAll<IEmailSender>();

            services.AddSingleton<TestPasswordResetEmailSender>();
            services.AddSingleton<TestDbCommandCounter>();
            services.AddSingleton<IEmailSender>(provider =>
                provider.GetRequiredService<TestPasswordResetEmailSender>());
            services.AddScoped<ISeeder, SuperAdminSeeder>();
            services.AddScoped<ISeeder, GovernorateSeeder>();
            services.AddScoped<ISeeder, CitySeeder>();
            services.AddScoped<ISeeder, AreaSeeder>();

            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            services.AddSingleton(connection);

            services.AddDbContext<LawyerPlatformDbContext>((serviceProvider, options) =>
                options
                    .UseSqlite(serviceProvider.GetRequiredService<SqliteConnection>())
                    .AddInterceptors(serviceProvider.GetRequiredService<TestDbCommandCounter>())
                    .UseBuildingBlockInterceptors(serviceProvider));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider
                .GetRequiredService<LawyerPlatformDbContext>()
                .Database
                .EnsureCreated();
        });
    }
}

public sealed class TestDbCommandCounter : DbCommandInterceptor
{
    private readonly ConcurrentQueue<string> _commands = new();

    public IReadOnlyCollection<string> Commands => _commands.ToArray();

    public void Reset()
    {
        while (_commands.TryDequeue(out _))
        {
        }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        _commands.Enqueue(command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        _commands.Enqueue(command.CommandText);
        return ValueTask.FromResult(result);
    }
}

public sealed class TestPasswordResetEmailSender : IEmailSender
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyCollection<EmailMessage> Messages => _messages.ToArray();
    public bool FailSending { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (FailSending)
        {
            throw new IOException("Simulated SMTP provider failure.");
        }

        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }
}
