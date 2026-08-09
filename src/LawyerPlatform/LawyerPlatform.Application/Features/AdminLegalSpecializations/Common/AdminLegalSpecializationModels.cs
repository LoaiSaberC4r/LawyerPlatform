using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.AdminLegalSpecializations.Common;

public sealed record AdminLegalSpecializationResponse(
    int Id,
    string NameAr,
    string NameEn,
    int DisplayOrder,
    bool IsActive,
    string RowVersion);

internal sealed record AdminLegalSpecializationSnapshot(
    int Id,
    string NameAr,
    string NameEn,
    int DisplayOrder,
    bool IsActive,
    byte[] RowVersion)
{
    public AdminLegalSpecializationResponse ToResponse()
        => new(Id, NameAr, NameEn, DisplayOrder, IsActive, RowVersionCodec.Encode(RowVersion));
}

internal sealed class LegalSpecializationByIdSpecification
    : Specification<LegalSpecialization, AdminLegalSpecializationSnapshot>
{
    public LegalSpecializationByIdSpecification(int id)
    {
        AddCriteria(item => item.Id == id);
        UseNoTracking();
        Select(item => new AdminLegalSpecializationSnapshot(
            item.Id,
            item.NameAr,
            item.NameEn,
            item.DisplayOrder,
            item.IsActive,
            item.RowVersion));
    }
}

internal sealed class LegalSpecializationIdsSpecification : Specification<LegalSpecialization, int>
{
    public LegalSpecializationIdsSpecification()
    {
        UseNoTracking();
        Select(item => item.Id);
    }
}

internal sealed record LegalSpecializationNameSnapshot(int Id, string NameAr, string NameEn);

internal sealed class LegalSpecializationNamesSpecification
    : Specification<LegalSpecialization, LegalSpecializationNameSnapshot>
{
    public LegalSpecializationNamesSpecification()
    {
        UseNoTracking();
        Select(item => new LegalSpecializationNameSnapshot(item.Id, item.NameAr, item.NameEn));
    }
}

internal static class LegalSpecializationNameConflictChecker
{
    public static async Task<Error?> FindConflictAsync(
        IReadRepository<LegalSpecialization, LawyerPlatformReadPersistence> repository,
        string nameAr,
        string nameEn,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var normalizedArabic = nameAr.Trim();
        var normalizedEnglish = nameEn.Trim();
        var names = await repository.ListAsync(
            new LegalSpecializationNamesSpecification(),
            cancellationToken);
        var candidates = excludedId.HasValue
            ? names.Where(item => item.Id != excludedId.Value)
            : names;
        var hasArabicConflict = candidates.Any(item =>
            string.Equals(item.NameAr, normalizedArabic, StringComparison.Ordinal));
        if (hasArabicConflict)
        {
            return LegalSpecializationErrors.DuplicateNameAr;
        }

        var hasEnglishConflict = candidates.Any(item =>
            string.Equals(item.NameEn, normalizedEnglish, StringComparison.OrdinalIgnoreCase));
        return hasEnglishConflict ? LegalSpecializationErrors.DuplicateNameEn : null;
    }
}
