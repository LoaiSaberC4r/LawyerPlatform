using LawyerPlatform.Application.Abstractions.Seeding;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Persistence;

internal sealed partial class DatabaseInitializationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<DatabaseInitializationOptions> options,
    ILogger<DatabaseInitializationHostedService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        InitializationStarted(logger);
        await using var scope = scopeFactory.CreateAsyncScope();
        if (options.Value.ApplyMigrationsOnStartup)
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        await scope.ServiceProvider.GetRequiredService<IEnsureSeeding>().SeedDatabaseAsync(cancellationToken);
        InitializationCompleted(logger);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    [LoggerMessage(EventId = 4120, Level = LogLevel.Information, Message = "Database initialization started.")]
    private static partial void InitializationStarted(ILogger logger);

    [LoggerMessage(EventId = 4121, Level = LogLevel.Information, Message = "Database initialization completed.")]
    private static partial void InitializationCompleted(ILogger logger);
}
