using BuildingBlock.Application.Repositories;
using LawyerPlatform.Application.Abstractions.Seeding;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;
using Microsoft.Extensions.Logging;

namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed class LegalSpecializationSeeder(
    IReadRepository<LegalSpecialization, LawyerPlatformReadPersistence> reader,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    ILogger<LegalSpecializationSeeder> logger)
    : ISeeder
{
    public int ExecutionOrder => SeedingOrder.LegalSpecializations;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (SeedCatalog.LegalSpecializations.Count == 0)
        {
            ReferenceSeederLog.NoApprovedData(logger, nameof(SeedCatalog.LegalSpecializations));
            return;
        }

        var added = false;
        foreach (var seed in SeedCatalog.LegalSpecializations)
        {
            var byId = await reader.GetByIdAsync(seed.Id, cancellationToken);
            if (byId is not null)
            {
                if (byId.NameAr == seed.NameAr && byId.NameEn == seed.NameEn && byId.DisplayOrder == seed.DisplayOrder)
                {
                    continue;
                }

                throw new InvalidOperationException("Legal specialization seed data conflicts with an existing row.");
            }

            if (await reader.AnyAsync(item => item.NameAr == seed.NameAr && item.NameEn == seed.NameEn, cancellationToken))
            {
                throw new InvalidOperationException("Legal specialization seed data conflicts with an existing row.");
            }

            var result = LegalSpecialization.Create(seed.Id, seed.NameAr, seed.NameEn, seed.DisplayOrder);
            if (result.IsFailure)
            {
                throw new InvalidOperationException("Legal specialization seed data is invalid.");
            }

            await unitOfWork.WriteRepository<LegalSpecialization>().AddAsync(result.Value, cancellationToken);
            added = true;
        }

        if (added)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
