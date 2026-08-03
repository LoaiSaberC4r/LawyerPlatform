using BuildingBlock.Application.Repositories;
using LawyerPlatform.Application.Abstractions.Seeding;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;
using Microsoft.Extensions.Logging;

namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed class AreaSeeder(
    IReadRepository<Area, LawyerPlatformReadPersistence> reader,
    IReadRepository<City, LawyerPlatformReadPersistence> cities,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    ILogger<AreaSeeder> logger)
    : ISeeder
{
    public int ExecutionOrder => SeedingOrder.Areas;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (SeedCatalog.Areas.Count == 0)
        {
            ReferenceSeederLog.NoApprovedData(logger, nameof(SeedCatalog.Areas));
            return;
        }

        var added = false;
        foreach (var seed in SeedCatalog.Areas)
        {
            if (!await cities.AnyAsync(item => item.Id == seed.CityId, cancellationToken))
            {
                throw new InvalidOperationException("Area seed data references a missing city.");
            }

            var byId = await reader.GetByIdAsync(seed.Id, cancellationToken);
            if (byId is not null)
            {
                if (byId.CityId == seed.CityId && byId.NameAr == seed.NameAr && byId.NameEn == seed.NameEn && byId.DisplayOrder == seed.DisplayOrder)
                {
                    continue;
                }

                throw new InvalidOperationException("Area seed data conflicts with an existing row.");
            }

            if (await reader.AnyAsync(item => item.CityId == seed.CityId && item.NameAr == seed.NameAr && item.NameEn == seed.NameEn, cancellationToken))
            {
                throw new InvalidOperationException("Area seed data conflicts with an existing row.");
            }

            var result = Area.Create(seed.Id, seed.CityId, seed.NameAr, seed.NameEn, seed.DisplayOrder);
            if (result.IsFailure)
            {
                throw new InvalidOperationException("Area seed data is invalid.");
            }

            await unitOfWork.WriteRepository<Area>().AddAsync(result.Value, cancellationToken);
            added = true;
        }

        if (added)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
