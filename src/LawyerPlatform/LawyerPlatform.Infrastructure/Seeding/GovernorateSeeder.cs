using BuildingBlock.Application.Repositories;
using LawyerPlatform.Application.Abstractions.Seeding;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;
using Microsoft.Extensions.Logging;

namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed class GovernorateSeeder(
    IReadRepository<Governorate, LawyerPlatformReadPersistence> reader,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    ILogger<GovernorateSeeder> logger)
    : ISeeder
{
    public int ExecutionOrder => SeedingOrder.Governorates;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (SeedCatalog.Governorates.Count == 0)
        {
            ReferenceSeederLog.NoApprovedData(logger, nameof(SeedCatalog.Governorates));
            return;
        }

        var added = false;
        foreach (var seed in SeedCatalog.Governorates)
        {
            var byId = await reader.GetByIdAsync(seed.Id, cancellationToken);
            if (byId is not null)
            {
                if (byId.NameAr == seed.NameAr && byId.NameEn == seed.NameEn && byId.DisplayOrder == seed.DisplayOrder)
                {
                    continue;
                }

                throw new InvalidOperationException("Governorate seed data conflicts with an existing row.");
            }

            if (await reader.AnyAsync(item => item.NameAr == seed.NameAr && item.NameEn == seed.NameEn, cancellationToken))
            {
                throw new InvalidOperationException("Governorate seed data conflicts with an existing row.");
            }

            var result = Governorate.Create(seed.Id, seed.NameAr, seed.NameEn, seed.DisplayOrder);
            if (result.IsFailure)
            {
                throw new InvalidOperationException("Governorate seed data is invalid.");
            }

            await unitOfWork.WriteRepository<Governorate>().AddAsync(result.Value, cancellationToken);
            added = true;
        }

        if (added)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
