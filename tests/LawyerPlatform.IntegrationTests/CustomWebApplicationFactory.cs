using BuildingBlock.Infrastructure.Bootstrap;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using LawyerPlatform.Infrastructure.Persistence;

namespace LawyerPlatform.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LawyerPlatformDbContext>>();
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
