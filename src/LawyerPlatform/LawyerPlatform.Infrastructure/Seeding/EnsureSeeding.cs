using LawyerPlatform.Application.Abstractions.Seeding;
using Microsoft.Extensions.Logging;

namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed partial class EnsureSeeding(
    IEnumerable<ISeeder> seeders,
    ILogger<EnsureSeeding> logger)
    : IEnsureSeeding
{
    public async Task SeedDatabaseAsync(CancellationToken cancellationToken)
    {
        foreach (var seeder in seeders.OrderBy(seeder => seeder.ExecutionOrder))
        {
            SeedingStarted(logger, seeder.GetType().Name);
            await seeder.SeedAsync(cancellationToken);
            SeedingCompleted(logger, seeder.GetType().Name);
        }
    }

    [LoggerMessage(EventId = 4100, Level = LogLevel.Information, Message = "Seeder {SeederName} started.")]
    private static partial void SeedingStarted(ILogger logger, string seederName);

    [LoggerMessage(EventId = 4101, Level = LogLevel.Information, Message = "Seeder {SeederName} completed.")]
    private static partial void SeedingCompleted(ILogger logger, string seederName);
}
