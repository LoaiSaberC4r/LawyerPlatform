using LawyerPlatform.Application.Abstractions.Seeding;

namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed class GovernorateSeeder(EgyptLocationSeedCoordinator coordinator) : ISeeder
{
    public int ExecutionOrder => SeedingOrder.Governorates;

    public Task SeedAsync(CancellationToken cancellationToken)
        => coordinator.SeedGovernoratesAsync(cancellationToken);
}
