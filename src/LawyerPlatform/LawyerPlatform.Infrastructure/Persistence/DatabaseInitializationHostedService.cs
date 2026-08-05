using LawyerPlatform.Application.Abstractions.Seeding;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Persistence;

internal sealed partial class DatabaseInitializationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<DatabaseInitializationOptions> options,
    IHostEnvironment environment,
    ILogger<DatabaseInitializationHostedService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        InitializationStarted(logger);
        await using var scope = scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        var migrationService = scope.ServiceProvider.GetRequiredService<IDatabaseMigrationService>();
        var connection = DatabaseConnectionDetails.From(dbContext);
        var applyMigrationsOnStartup = options.Value.ApplyMigrationsOnStartup;
        var aspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "<not set>";
        var dotNetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "<not set>";

        EffectiveEnvironment(logger, environment.EnvironmentName);
        AspNetCoreEnvironment(logger, aspNetCoreEnvironment);
        DotNetEnvironment(logger, dotNetEnvironment);
        ApplyMigrationsOnStartupResolved(logger, applyMigrationsOnStartup);
        DatabaseServer(logger, connection.DataSource);
        DatabaseName(logger, connection.Database);
        IntegratedSecurity(logger, connection.IntegratedSecurity);

        if (!applyMigrationsOnStartup)
        {
            InitializationSkipped(logger);
            return;
        }

        try
        {
            var compiledMigrations = migrationService.GetCompiledMigrations(dbContext);
            CompiledMigrationsDiscovered(logger, compiledMigrations.Count);

            var pendingMigrations = await migrationService.GetPendingMigrationsAsync(dbContext, cancellationToken);
            PendingMigrationsDiscovered(logger, pendingMigrations.Count);

            MigrationStarted(logger);
            await migrationService.MigrateAsync(dbContext, cancellationToken);
            MigrationCompleted(logger);
        }
        catch (Exception)
        {
            MigrationFailed(logger);
            SeedingWillNotRun(logger);
            throw;
        }

        DatabaseSeedingStarted(logger);
        try
        {
            await scope.ServiceProvider.GetRequiredService<IEnsureSeeding>().SeedDatabaseAsync(cancellationToken);
        }
        catch (Exception)
        {
            DatabaseSeedingFailed(logger);
            throw;
        }

        DatabaseSeedingCompleted(logger);
        InitializationCompleted(logger);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    [LoggerMessage(EventId = 4120, Level = LogLevel.Information, Message = "Database initialization started.")]
    private static partial void InitializationStarted(ILogger logger);

    [LoggerMessage(EventId = 4121, Level = LogLevel.Information, Message = "Database initialization completed.")]
    private static partial void InitializationCompleted(ILogger logger);

    [LoggerMessage(EventId = 4122, Level = LogLevel.Information, Message = "Effective environment: {EnvironmentName}.")]
    private static partial void EffectiveEnvironment(ILogger logger, string environmentName);

    [LoggerMessage(EventId = 4123, Level = LogLevel.Information, Message = "ASPNETCORE_ENVIRONMENT environment variable: {EnvironmentName}.")]
    private static partial void AspNetCoreEnvironment(ILogger logger, string environmentName);

    [LoggerMessage(EventId = 4124, Level = LogLevel.Information, Message = "DOTNET_ENVIRONMENT environment variable: {EnvironmentName}.")]
    private static partial void DotNetEnvironment(ILogger logger, string environmentName);

    [LoggerMessage(EventId = 4125, Level = LogLevel.Information, Message = "ApplyMigrationsOnStartup resolved to {ApplyMigrationsOnStartup}.")]
    private static partial void ApplyMigrationsOnStartupResolved(ILogger logger, bool applyMigrationsOnStartup);

    [LoggerMessage(EventId = 4126, Level = LogLevel.Information, Message = "Database server: {DataSource}.")]
    private static partial void DatabaseServer(ILogger logger, string dataSource);

    [LoggerMessage(EventId = 4127, Level = LogLevel.Information, Message = "Database name: {Database}.")]
    private static partial void DatabaseName(ILogger logger, string database);

    [LoggerMessage(EventId = 4128, Level = LogLevel.Information, Message = "Integrated security enabled: {IntegratedSecurity}.")]
    private static partial void IntegratedSecurity(ILogger logger, bool integratedSecurity);

    [LoggerMessage(EventId = 4129, Level = LogLevel.Information, Message = "Database migration and seeding skipped because ApplyMigrationsOnStartup is disabled.")]
    private static partial void InitializationSkipped(ILogger logger);

    [LoggerMessage(EventId = 4130, Level = LogLevel.Information, Message = "Compiled migrations discovered: {MigrationCount}.")]
    private static partial void CompiledMigrationsDiscovered(ILogger logger, int migrationCount);

    [LoggerMessage(EventId = 4131, Level = LogLevel.Information, Message = "Pending migrations discovered: {MigrationCount}.")]
    private static partial void PendingMigrationsDiscovered(ILogger logger, int migrationCount);

    [LoggerMessage(EventId = 4132, Level = LogLevel.Information, Message = "Database migration started.")]
    private static partial void MigrationStarted(ILogger logger);

    [LoggerMessage(EventId = 4133, Level = LogLevel.Information, Message = "Database migration completed.")]
    private static partial void MigrationCompleted(ILogger logger);

    [LoggerMessage(EventId = 4134, Level = LogLevel.Error, Message = "Database migration failed.")]
    private static partial void MigrationFailed(ILogger logger);

    [LoggerMessage(EventId = 4135, Level = LogLevel.Error, Message = "Database seeding will not run.")]
    private static partial void SeedingWillNotRun(ILogger logger);

    [LoggerMessage(EventId = 4136, Level = LogLevel.Information, Message = "Database seeding started.")]
    private static partial void DatabaseSeedingStarted(ILogger logger);

    [LoggerMessage(EventId = 4137, Level = LogLevel.Information, Message = "Database seeding completed.")]
    private static partial void DatabaseSeedingCompleted(ILogger logger);

    [LoggerMessage(EventId = 4138, Level = LogLevel.Error, Message = "Database seeding failed.")]
    private static partial void DatabaseSeedingFailed(ILogger logger);
}
