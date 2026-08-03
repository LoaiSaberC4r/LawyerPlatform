namespace LawyerPlatform.Application.Abstractions.Seeding;

public interface IEnsureSeeding
{
    Task SeedDatabaseAsync(CancellationToken cancellationToken);
}
