using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace LawyerPlatform.Infrastructure.Persistence;

public sealed class LawyerPlatformDbContextFactory
    : IDesignTimeDbContextFactory<LawyerPlatformDbContext>
{
    public LawyerPlatformDbContext CreateDbContext(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var environment =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";

        var apiProjectPath = ResolveApiProjectPath();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile(
                "appsettings.json",
                optional: false,
                reloadOnChange: false)
            .AddJsonFile(
                $"appsettings.{environment}.json",
                optional: true,
                reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");

        var migrationsAssemblyName =
            typeof(LawyerPlatformDbContext)
                .Assembly
                .GetName()
                .Name
            ?? throw new InvalidOperationException(
                "Unable to resolve the migrations assembly name.");

        var optionsBuilder =
            new DbContextOptionsBuilder<LawyerPlatformDbContext>();

        optionsBuilder.UseSqlServer(
            connectionString,
            sqlServerOptions =>
                sqlServerOptions.MigrationsAssembly(
                    migrationsAssemblyName));

        return new LawyerPlatformDbContext(
            optionsBuilder.Options);
    }

    private static string ResolveApiProjectPath()
    {
        var directory = new DirectoryInfo(
            Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            var directCandidate = Path.Combine(
                directory.FullName,
                "LawyerPlatform.Api");

            if (File.Exists(
                Path.Combine(
                    directCandidate,
                    "appsettings.json")))
            {
                return directCandidate;
            }

            var solutionCandidate = Path.Combine(
                directory.FullName,
                "src",
                "LawyerPlatform",
                "LawyerPlatform.Api");

            if (File.Exists(
                Path.Combine(
                    solutionCandidate,
                    "appsettings.json")))
            {
                return solutionCandidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Unable to locate LawyerPlatform.Api/appsettings.json.");
    }
}
