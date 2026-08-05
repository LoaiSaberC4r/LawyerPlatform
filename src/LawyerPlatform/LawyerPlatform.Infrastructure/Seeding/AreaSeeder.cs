using LawyerPlatform.Application.Abstractions.Seeding;

namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed class AreaSeeder(EgyptLocationSeedCoordinator coordinator) : ISeeder
{
    public int ExecutionOrder => SeedingOrder.Areas;

    public Task SeedAsync(CancellationToken cancellationToken)
        => coordinator.SeedAreasAsync(cancellationToken);
}
