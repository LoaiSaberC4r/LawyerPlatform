using BuildingBlock.Infrastructure.Bootstrap;
using LawyerPlatform.Application.Abstractions.Seeding;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LawyerPlatform.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public async Task SeedDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<IEnsureSeeding>()
            .SeedDatabaseAsync(cancellationToken);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
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
                ["ConsultationScheduling:TimeZoneId"] = "Africa/Cairo",
                ["DatabaseInitialization:ApplyMigrationsOnStartup"] = "false",
                ["Cors:AllowAnyOrigin"] = "false",
                ["Cors:AllowCredentials"] = "false",
                ["Cors:AllowedOrigins:0"] = "http://localhost:4200",
                ["Cors:AllowedOrigins:1"] = "https://localhost:4200",
                ["MediaStorage:RootPath"] = Path.Combine(Path.GetTempPath(), "LawyerPlatformTests", Guid.NewGuid().ToString("N")),
                ["MediaStorage:MaxFileSizeBytes"] = "1048576",
                ["MediaStorage:AllowedExtensions:0"] = ".jpg",
                ["MediaStorage:AllowedExtensions:1"] = ".jpeg",
                ["MediaStorage:AllowedExtensions:2"] = ".png",
                ["MediaStorage:AllowedExtensions:3"] = ".pdf",
                ["MediaStorage:AllowedMimeTypes:0"] = "image/jpeg",
                ["MediaStorage:AllowedMimeTypes:1"] = "image/png",
                ["MediaStorage:AllowedMimeTypes:2"] = "application/pdf",
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

            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            services.AddSingleton(connection);

            services.AddDbContext<LawyerPlatformDbContext>((serviceProvider, options) =>
                options
                    .UseSqlite(serviceProvider.GetRequiredService<SqliteConnection>())
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
