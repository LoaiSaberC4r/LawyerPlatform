using LawyerPlatform.Application.Abstractions.Seeding;

namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed class CitySeeder(EgyptLocationSeedCoordinator coordinator) : ISeeder
{
    public int ExecutionOrder => SeedingOrder.Cities;

    public Task SeedAsync(CancellationToken cancellationToken)
        => coordinator.SeedCitiesAsync(cancellationToken);
}
