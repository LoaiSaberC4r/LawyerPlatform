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
        var orderedSeeders = seeders
            .OrderBy(seeder => seeder.ExecutionOrder)
            .ThenBy(seeder => seeder.GetType().FullName, StringComparer.Ordinal);

        foreach (var seeder in orderedSeeders)
        {
            var seederName = seeder.GetType().Name;
            SeedingStarted(logger, seederName);
            try
            {
                await seeder.SeedAsync(cancellationToken);
            }
            catch (Exception)
            {
                SeedingFailed(logger, seederName);
                throw;
            }

            SeedingCompleted(logger, seederName);
        }
    }

    [LoggerMessage(EventId = 4100, Level = LogLevel.Information, Message = "Seeder {SeederName} started.")]
    private static partial void SeedingStarted(ILogger logger, string seederName);

    [LoggerMessage(EventId = 4101, Level = LogLevel.Information, Message = "Seeder {SeederName} completed.")]
    private static partial void SeedingCompleted(ILogger logger, string seederName);

    [LoggerMessage(EventId = 4102, Level = LogLevel.Error, Message = "Seeder {SeederName} failed.")]
    private static partial void SeedingFailed(ILogger logger, string seederName);
}
