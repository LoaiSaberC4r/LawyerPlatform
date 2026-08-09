using System.Text;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Specification;
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

        var existing = await reader.ListAsync(
            new ExistingLegalSpecializationsSpecification(),
            cancellationToken);
        var existingById = existing.ToDictionary(item => item.Id);
        var existingArabicNames = BuildNameOwners(existing, item => item.NameAr, StringComparer.Ordinal);
        var existingEnglishNames = BuildNameOwners(existing, item => item.NameEn, StringComparer.OrdinalIgnoreCase);
        var approvedCatalogWasPreviouslySeeded = IsCompleteApprovedCatalog(existingById);
        var missing = new List<LegalSpecialization>(SeedCatalog.LegalSpecializations.Count);

        foreach (var seed in SeedCatalog.LegalSpecializations)
        {
            EnsureNameIsAvailable(
                existingArabicNames,
                seed.NameAr,
                seed.Id,
                LegalSpecializationSeedConflictKind.ArabicName);
            EnsureNameIsAvailable(
                existingEnglishNames,
                seed.NameEn,
                seed.Id,
                LegalSpecializationSeedConflictKind.EnglishName);

            if (existingById.TryGetValue(seed.Id, out var current))
            {
                var matchesApprovedArabicName = StringComparer.Ordinal.Equals(
                    NormalizeName(current.NameAr),
                    NormalizeName(seed.NameAr));
                var matchesApprovedEnglishName = StringComparer.OrdinalIgnoreCase.Equals(
                    NormalizeName(current.NameEn),
                    NormalizeName(seed.NameEn));
                if (!matchesApprovedArabicName && !matchesApprovedEnglishName && !approvedCatalogWasPreviouslySeeded)
                {
                    throw new LegalSpecializationSeedConflictException(
                        LegalSpecializationSeedConflictKind.Id,
                        seed.Id,
                        "A LegalSpecialization seed ID conflicts with an unrelated existing row.");
                }

                // Existing rows are authoritative after their initial insertion. This deliberately
                // preserves later name, display-order, and activation changes made by SuperAdmin.
                continue;
            }

            var result = LegalSpecialization.Create(seed.Id, seed.NameAr, seed.NameEn, seed.DisplayOrder);
            if (result.IsFailure)
            {
                throw new InvalidOperationException("Legal specialization seed data is invalid.");
            }

            missing.Add(result.Value);
        }

        if (missing.Count == 0)
        {
            return;
        }

        var writer = unitOfWork.WriteRepository<LegalSpecialization>();
        foreach (var specialization in missing)
        {
            await writer.AddAsync(specialization, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, int> BuildNameOwners(
        IEnumerable<LegalSpecializationSeedSnapshot> records,
        Func<LegalSpecializationSeedSnapshot, string> nameSelector,
        StringComparer comparer)
    {
        var owners = new Dictionary<string, int>(comparer);
        foreach (var record in records)
        {
            var name = NormalizeName(nameSelector(record));
            if (!owners.TryAdd(name, record.Id) && owners[name] != record.Id)
            {
                owners[name] = 0;
            }
        }

        return owners;
    }

    private static bool IsCompleteApprovedCatalog(
        Dictionary<int, LegalSpecializationSeedSnapshot> existingById)
        => SeedCatalog.LegalSpecializations.All(seed => existingById.ContainsKey(seed.Id));

    private static void EnsureNameIsAvailable(
        Dictionary<string, int> owners,
        string approvedName,
        int seedId,
        LegalSpecializationSeedConflictKind conflictKind)
    {
        if (owners.TryGetValue(NormalizeName(approvedName), out var ownerId) && ownerId != seedId)
        {
            var field = conflictKind == LegalSpecializationSeedConflictKind.ArabicName
                ? "Arabic"
                : "English";
            throw new LegalSpecializationSeedConflictException(
                conflictKind,
                seedId,
                $"A normalized LegalSpecialization {field} seed name belongs to a different existing row.");
        }
    }

    private static string NormalizeName(string name)
        => name.Trim().Normalize(NormalizationForm.FormC);
}

internal sealed class ExistingLegalSpecializationsSpecification
    : Specification<LegalSpecialization, LegalSpecializationSeedSnapshot>
{
    public ExistingLegalSpecializationsSpecification()
    {
        Select(item => new LegalSpecializationSeedSnapshot(item.Id, item.NameAr, item.NameEn));
        UseNoTracking();
    }
}

internal sealed record LegalSpecializationSeedSnapshot(int Id, string NameAr, string NameEn);
