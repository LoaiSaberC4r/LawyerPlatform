using BuildingBlock.Infrastructure.Bootstrap;
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
                ["DatabaseInitialization:ApplyMigrationsOnStartup"] = "false"
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
