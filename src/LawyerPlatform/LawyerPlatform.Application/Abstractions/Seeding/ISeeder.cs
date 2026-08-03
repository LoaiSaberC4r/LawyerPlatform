namespace LawyerPlatform.Application.Abstractions.Seeding;

public interface ISeeder
{
    int ExecutionOrder { get; }
    Task SeedAsync(CancellationToken cancellationToken);
}
