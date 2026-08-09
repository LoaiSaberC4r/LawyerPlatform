using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.Lawyers.Common;

internal sealed class LawyerAggregateCompletionService(
    IReadRepository<Area, LawyerPlatformReadPersistence> areaReader,
    IReadRepository<LegalSpecialization, LawyerPlatformReadPersistence> specializationReader,
    ILawyerDocumentPolicy documentPolicy)
{
    public async Task<LawyerProfileCompletionResponse> CalculateAsync(
        LawyerProfile profile,
        CancellationToken cancellationToken)
    {
        var office = profile.Offices.SingleOrDefault(item => item.IsPrimary && item.IsActive);
        var officeComplete = false;
        if (office is not null && !string.IsNullOrWhiteSpace(office.DetailedAddress))
        {
            officeComplete = await areaReader.AnyAsync(
                area => area.Id == office.AreaId && area.IsActive &&
                        area.CityId == office.CityId && area.City.IsActive &&
                        area.City.GovernorateId == office.GovernorateId && area.City.Governorate.IsActive,
                cancellationToken);
        }

        var specializationIds = profile.Specializations.Select(item => item.LegalSpecializationId).ToArray();
        var activeSpecializations = specializationIds.Length == 0
            ? 0L
            : await specializationReader.LongCountAsync(
                specialization => specializationIds.Contains(specialization.Id) && specialization.IsActive,
                cancellationToken);

        return LawyerProfileCompletionCalculator.Calculate(
            profile.ApprovalStatus,
            profile.FullName,
            profile.ProfessionalTitle,
            profile.YearsOfExperience,
            profile.ProfessionalRegistrationNumber,
            officeComplete,
            activeSpecializations > 0,
            profile.Documents.Where(document => !document.IsDeleted).Select(document => document.DocumentType).ToArray(),
            profile.UserAccount.Status == LawyerPlatform.Domain.Accounts.AccountStatus.Active,
            documentPolicy);
    }
}

internal sealed class LocationHierarchySpecification : Specification<Area, LocationHierarchySnapshot>
{
    public LocationHierarchySpecification(int areaId)
    {
        AddCriteria(area => area.Id == areaId);
        UseNoTracking();
        Select(area => new LocationHierarchySnapshot(
            area.Id,
            area.IsActive,
            area.CityId,
            area.City.IsActive,
            area.City.GovernorateId,
            area.City.Governorate.IsActive));
    }
}

internal sealed record LocationHierarchySnapshot(
    int AreaId,
    bool AreaIsActive,
    int CityId,
    bool CityIsActive,
    int GovernorateId,
    bool GovernorateIsActive);

internal sealed class RequestedSpecializationsSpecification : Specification<LegalSpecialization, RequestedSpecializationSnapshot>
{
    public RequestedSpecializationsSpecification(IReadOnlyCollection<int> ids)
    {
        AddCriteria(specialization => ids.Contains(specialization.Id));
        UseNoTracking();
        Select(specialization => new RequestedSpecializationSnapshot(specialization.Id, specialization.IsActive));
    }
}

internal sealed record RequestedSpecializationSnapshot(int Id, bool IsActive);
