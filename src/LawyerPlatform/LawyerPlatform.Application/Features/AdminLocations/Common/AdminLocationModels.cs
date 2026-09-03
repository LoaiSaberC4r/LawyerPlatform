using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.AdminLocations.Common;

public sealed record AdminGovernorateResponse(
    int Id, string NameAr, string NameEn, int DisplayOrder, bool IsActive, string RowVersion);

public sealed record AdminCityResponse(
    int Id, int GovernorateId, string GovernorateNameAr, string GovernorateNameEn,
    string NameAr, string NameEn, int DisplayOrder, bool IsActive, string RowVersion);

public sealed record AdminAreaResponse(
    int Id, int GovernorateId, string GovernorateNameAr, string GovernorateNameEn,
    int CityId, string CityNameAr, string CityNameEn,
    string NameAr, string NameEn, int DisplayOrder, bool IsActive, string RowVersion);

internal sealed record GovernorateSnapshot(
    int Id, string NameAr, string NameEn, int DisplayOrder, bool IsActive, byte[] RowVersion)
{
    public AdminGovernorateResponse ToResponse()
        => new(Id, NameAr, NameEn, DisplayOrder, IsActive, RowVersionCodec.Encode(RowVersion));
}

internal sealed record CitySnapshot(
    int Id, int GovernorateId, string GovernorateNameAr, string GovernorateNameEn,
    string NameAr, string NameEn, int DisplayOrder, bool IsActive, byte[] RowVersion)
{
    public AdminCityResponse ToResponse() => new(
        Id, GovernorateId, GovernorateNameAr, GovernorateNameEn,
        NameAr, NameEn, DisplayOrder, IsActive, RowVersionCodec.Encode(RowVersion));
}

internal sealed record AreaSnapshot(
    int Id, int GovernorateId, string GovernorateNameAr, string GovernorateNameEn,
    int CityId, string CityNameAr, string CityNameEn,
    string NameAr, string NameEn, int DisplayOrder, bool IsActive, byte[] RowVersion)
{
    public AdminAreaResponse ToResponse() => new(
        Id, GovernorateId, GovernorateNameAr, GovernorateNameEn,
        CityId, CityNameAr, CityNameEn, NameAr, NameEn,
        DisplayOrder, IsActive, RowVersionCodec.Encode(RowVersion));
}

internal sealed record LocationNameSnapshot(int Id, int? ParentId, string NameAr, string NameEn);
internal sealed record GovernorateNameSnapshot(int Id, string NameAr, string NameEn);
internal sealed record CityParentSnapshot(
    int Id, string NameAr, string NameEn,
    int GovernorateId, string GovernorateNameAr, string GovernorateNameEn);

internal sealed class GovernorateByIdSpecification : Specification<Governorate, GovernorateSnapshot>
{
    public GovernorateByIdSpecification(int id)
    {
        AddCriteria(item => item.Id == id);
        UseNoTracking();
        Select(item => new GovernorateSnapshot(
            item.Id, item.NameAr, item.NameEn, item.DisplayOrder, item.IsActive, item.RowVersion));
    }
}

internal sealed class CityByIdSpecification : Specification<City, CitySnapshot>
{
    public CityByIdSpecification(int id)
    {
        AddCriteria(item => item.Id == id);
        UseNoTracking();
        Select(item => new CitySnapshot(
            item.Id, item.GovernorateId, item.Governorate.NameAr, item.Governorate.NameEn,
            item.NameAr, item.NameEn, item.DisplayOrder, item.IsActive, item.RowVersion));
    }
}

internal sealed class AreaByIdSpecification : Specification<Area, AreaSnapshot>
{
    public AreaByIdSpecification(int id)
    {
        AddCriteria(item => item.Id == id);
        UseNoTracking();
        Select(item => new AreaSnapshot(
            item.Id, item.City.GovernorateId, item.City.Governorate.NameAr, item.City.Governorate.NameEn,
            item.CityId, item.City.NameAr, item.City.NameEn,
            item.NameAr, item.NameEn, item.DisplayOrder, item.IsActive, item.RowVersion));
    }
}

internal sealed class GovernorateNameConflictSpecification : Specification<Governorate, LocationNameSnapshot>
{
    public GovernorateNameConflictSpecification(string nameAr, string nameEn, int? excludedId)
    {
        var normalizedNameAr = nameAr.Trim();
        var normalizedNameEn = nameEn.Trim();
        AddCriteria(item =>
            (!excludedId.HasValue || item.Id != excludedId.Value) &&
            (item.NameAr == normalizedNameAr || item.NameEn == normalizedNameEn));
        UseNoTracking();
        Select(item => new LocationNameSnapshot(item.Id, null, item.NameAr, item.NameEn));
    }
}

internal sealed class CityNameConflictSpecification : Specification<City, LocationNameSnapshot>
{
    public CityNameConflictSpecification(
        int governorateId,
        string nameAr,
        string nameEn,
        int? excludedId)
    {
        var normalizedNameAr = nameAr.Trim();
        var normalizedNameEn = nameEn.Trim();
        AddCriteria(item =>
            item.GovernorateId == governorateId &&
            (!excludedId.HasValue || item.Id != excludedId.Value) &&
            (item.NameAr == normalizedNameAr || item.NameEn == normalizedNameEn));
        UseNoTracking();
        Select(item => new LocationNameSnapshot(item.Id, item.GovernorateId, item.NameAr, item.NameEn));
    }
}

internal sealed class AreaNameConflictSpecification : Specification<Area, LocationNameSnapshot>
{
    public AreaNameConflictSpecification(int cityId, string nameAr, string nameEn, int? excludedId)
    {
        var normalizedNameAr = nameAr.Trim();
        var normalizedNameEn = nameEn.Trim();
        AddCriteria(item =>
            item.CityId == cityId &&
            (!excludedId.HasValue || item.Id != excludedId.Value) &&
            (item.NameAr == normalizedNameAr || item.NameEn == normalizedNameEn));
        UseNoTracking();
        Select(item => new LocationNameSnapshot(item.Id, item.CityId, item.NameAr, item.NameEn));
    }
}

internal sealed class GovernorateNameByIdSpecification : Specification<Governorate, GovernorateNameSnapshot>
{
    public GovernorateNameByIdSpecification(int id)
    {
        AddCriteria(item => item.Id == id);
        UseNoTracking();
        Select(item => new GovernorateNameSnapshot(item.Id, item.NameAr, item.NameEn));
    }
}

internal sealed class CityParentByIdSpecification : Specification<City, CityParentSnapshot>
{
    public CityParentByIdSpecification(int id)
    {
        AddCriteria(item => item.Id == id);
        UseNoTracking();
        Select(item => new CityParentSnapshot(
            item.Id, item.NameAr, item.NameEn,
            item.GovernorateId, item.Governorate.NameAr, item.Governorate.NameEn));
    }
}

internal static class LocationConflictChecker
{
    public static async Task<Error?> FindGovernorateConflictAsync(
        IReadRepository<Governorate, LawyerPlatformReadPersistence> repository,
        string nameAr, string nameEn, int? excludedId, CancellationToken cancellationToken)
        => FindConflict(
            await repository.ListAsync(
                new GovernorateNameConflictSpecification(nameAr, nameEn, excludedId),
                cancellationToken),
            null, nameAr, nameEn, excludedId,
            GovernorateErrors.DuplicateNameAr, GovernorateErrors.DuplicateNameEn);

    public static async Task<Error?> FindCityConflictAsync(
        IReadRepository<City, LawyerPlatformReadPersistence> repository,
        int governorateId, string nameAr, string nameEn, int? excludedId, CancellationToken cancellationToken)
        => FindConflict(
            await repository.ListAsync(
                new CityNameConflictSpecification(governorateId, nameAr, nameEn, excludedId),
                cancellationToken),
            governorateId, nameAr, nameEn, excludedId,
            CityErrors.DuplicateNameAr, CityErrors.DuplicateNameEn);

    public static async Task<Error?> FindAreaConflictAsync(
        IReadRepository<Area, LawyerPlatformReadPersistence> repository,
        int cityId, string nameAr, string nameEn, int? excludedId, CancellationToken cancellationToken)
        => FindConflict(
            await repository.ListAsync(
                new AreaNameConflictSpecification(cityId, nameAr, nameEn, excludedId),
                cancellationToken),
            cityId, nameAr, nameEn, excludedId,
            AreaErrors.DuplicateNameAr, AreaErrors.DuplicateNameEn);

    private static Error? FindConflict(
        IReadOnlyList<LocationNameSnapshot> names,
        int? parentId,
        string nameAr,
        string nameEn,
        int? excludedId,
        Error duplicateArabic,
        Error duplicateEnglish)
    {
        var candidates = names.Where(item => item.Id != excludedId && item.ParentId == parentId);
        if (candidates.Any(item => string.Equals(item.NameAr, nameAr.Trim(), StringComparison.Ordinal)))
            return duplicateArabic;
        return candidates.Any(item => string.Equals(item.NameEn, nameEn.Trim(), StringComparison.OrdinalIgnoreCase))
            ? duplicateEnglish
            : null;
    }
}
