using BuildingBlock.Application.Repositories;
using LawyerPlatform.Application.Abstractions.Seeding;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;
using Microsoft.Extensions.Logging;

namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed class CitySeeder(
    IReadRepository<City, LawyerPlatformReadPersistence> reader,
    IReadRepository<Governorate, LawyerPlatformReadPersistence> governorates,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    ILogger<CitySeeder> logger)
    : ISeeder
{
    public int ExecutionOrder => SeedingOrder.Cities;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (SeedCatalog.Cities.Count == 0)
        {
            ReferenceSeederLog.NoApprovedData(logger, nameof(SeedCatalog.Cities));
            return;
        }

        var added = false;
        foreach (var seed in SeedCatalog.Cities)
        {
            if (!await governorates.AnyAsync(item => item.Id == seed.GovernorateId, cancellationToken))
            {
                throw new InvalidOperationException("City seed data references a missing governorate.");
            }

            var byId = await reader.GetByIdAsync(seed.Id, cancellationToken);
            if (byId is not null)
            {
                if (byId.GovernorateId == seed.GovernorateId && byId.NameAr == seed.NameAr && byId.NameEn == seed.NameEn && byId.DisplayOrder == seed.DisplayOrder)
                {
                    continue;
                }

                throw new InvalidOperationException("City seed data conflicts with an existing row.");
            }

            if (await reader.AnyAsync(item => item.GovernorateId == seed.GovernorateId && item.NameAr == seed.NameAr && item.NameEn == seed.NameEn, cancellationToken))
            {
                throw new InvalidOperationException("City seed data conflicts with an existing row.");
            }

            var result = City.Create(seed.Id, seed.GovernorateId, seed.NameAr, seed.NameEn, seed.DisplayOrder);
            if (result.IsFailure)
            {
                throw new InvalidOperationException("City seed data is invalid.");
            }

            await unitOfWork.WriteRepository<City>().AddAsync(result.Value, cancellationToken);
            added = true;
        }

        if (added)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
