using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Application.Features.Lawyers.GetOwnProfile;

public sealed record GetOwnProfileQuery : IQuery<LawyerOwnProfileResponse>;

public sealed record LawyerOwnProfileResponse(
    Guid Id,
    Guid UserAccountId,
    string FullName,
    string? ProfessionalTitle,
    string? Biography,
    int? YearsOfExperience,
    string? ProfessionalRegistrationNumber,
    bool HasProfileImage,
    string? ProfileImageContentUrl,
    string ApprovalStatus,
    string AccountStatus,
    LawyerOfficeResponse? PrimaryOffice,
    IReadOnlyList<LawyerSpecializationResponse> Specializations,
    int DocumentCount,
    LawyerProfileCompletionResponse Completion,
    string RowVersion);

internal sealed class GetOwnProfileQueryHandler(
    ICurrentUser currentUser,
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> repository,
    ILawyerDocumentPolicy documentPolicy)
    : IQueryHandler<GetOwnProfileQuery, LawyerOwnProfileResponse>
{
    public async Task<Result<LawyerOwnProfileResponse>> Handle(GetOwnProfileQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<LawyerOwnProfileResponse>.Fail(LawyerApplicationErrors.AccountNotFound);
        }

        var snapshot = await repository.FirstOrDefaultAsync(new OwnProfileSpecification(userId), cancellationToken);
        return snapshot is null
            ? Result<LawyerOwnProfileResponse>.Fail(LawyerErrors.NotFound)
            : Result<LawyerOwnProfileResponse>.Ok(OwnProfileMapper.Map(snapshot, documentPolicy));
    }
}

internal static class OwnProfileMapper
{
    public static LawyerOwnProfileResponse Map(OwnProfileSnapshot snapshot, ILawyerDocumentPolicy documentPolicy)
    {
        var completion = LawyerProfileCompletionCalculator.Calculate(
            snapshot.ApprovalStatus,
            snapshot.FullName,
            snapshot.ProfessionalTitle,
            snapshot.YearsOfExperience,
            snapshot.ProfessionalRegistrationNumber,
            snapshot.OfficeComplete,
            snapshot.Specializations.Any(item => item.IsActive),
            snapshot.ActiveDocumentTypes,
            snapshot.AccountStatus == AccountStatus.Active,
            documentPolicy);

        return new LawyerOwnProfileResponse(
            snapshot.Id,
            snapshot.UserAccountId,
            snapshot.FullName,
            snapshot.ProfessionalTitle,
            snapshot.Biography,
            snapshot.YearsOfExperience,
            snapshot.ProfessionalRegistrationNumber,
            snapshot.HasProfileImage,
            snapshot.HasProfileImage ? "/api/v1/lawyer/profile/image" : null,
            snapshot.ApprovalStatus.ToString(),
            snapshot.AccountStatus.ToString(),
            snapshot.PrimaryOffice is null ? null : new LawyerOfficeResponse(
                snapshot.PrimaryOffice.Id,
                snapshot.PrimaryOffice.GovernorateId,
                snapshot.PrimaryOffice.GovernorateNameAr,
                snapshot.PrimaryOffice.GovernorateNameEn,
                snapshot.PrimaryOffice.CityId,
                snapshot.PrimaryOffice.CityNameAr,
                snapshot.PrimaryOffice.CityNameEn,
                snapshot.PrimaryOffice.AreaId,
                snapshot.PrimaryOffice.AreaNameAr,
                snapshot.PrimaryOffice.AreaNameEn,
                snapshot.PrimaryOffice.DetailedAddress,
                snapshot.PrimaryOffice.PublicPhoneNumber,
                RowVersionCodec.Encode(snapshot.PrimaryOffice.RowVersion)),
            snapshot.Specializations.Select(item => new LawyerSpecializationResponse(item.Id, item.NameAr, item.NameEn)).ToArray(),
            snapshot.DocumentCount,
            completion,
            RowVersionCodec.Encode(snapshot.RowVersion));
    }
}

internal sealed class OwnProfileSpecification : Specification<LawyerProfile, OwnProfileSnapshot>
{
    public OwnProfileSpecification(Guid userAccountId)
    {
        AddCriteria(profile => profile.UserAccountId == userAccountId);
        UseNoTracking();
        UseSplitQuery();
        Select(profile => new OwnProfileSnapshot(
            profile.Id,
            profile.UserAccountId,
            profile.FullName,
            profile.ProfessionalTitle,
            profile.Biography,
            profile.YearsOfExperience,
            profile.ProfessionalRegistrationNumber,
            profile.ProfileImageStorageKey != null,
            profile.ApprovalStatus,
            profile.UserAccount.Status,
            profile.Offices
                .Where(office => office.IsPrimary && office.IsActive)
                .Select(office => new OwnOfficeSnapshot(
                    office.Id,
                    office.GovernorateId,
                    office.Governorate.NameAr,
                    office.Governorate.NameEn,
                    office.CityId,
                    office.City.NameAr,
                    office.City.NameEn,
                    office.AreaId,
                    office.Area.NameAr,
                    office.Area.NameEn,
                    office.DetailedAddress,
                    office.PublicPhoneNumber,
                    office.RowVersion))
                .FirstOrDefault(),
            profile.Offices.Any(office =>
                office.IsPrimary && office.IsActive &&
                office.Governorate.IsActive && office.City.IsActive && office.Area.IsActive &&
                office.City.GovernorateId == office.GovernorateId && office.Area.CityId == office.CityId &&
                office.DetailedAddress != ""),
            profile.Specializations
                .OrderBy(item => item.LegalSpecialization.DisplayOrder)
                .ThenBy(item => item.LegalSpecializationId)
                .Select(item => new OwnSpecializationSnapshot(
                    item.LegalSpecializationId,
                    item.LegalSpecialization.NameAr,
                    item.LegalSpecialization.NameEn,
                    item.LegalSpecialization.IsActive))
                .ToArray(),
            profile.Documents.Count(document => !document.IsDeleted),
            profile.Documents.Where(document => !document.IsDeleted).Select(document => document.DocumentType).ToArray(),
            profile.RowVersion));
    }
}

internal sealed record OwnProfileSnapshot(
    Guid Id,
    Guid UserAccountId,
    string FullName,
    string? ProfessionalTitle,
    string? Biography,
    int? YearsOfExperience,
    string? ProfessionalRegistrationNumber,
    bool HasProfileImage,
    LawyerApprovalStatus ApprovalStatus,
    AccountStatus AccountStatus,
    OwnOfficeSnapshot? PrimaryOffice,
    bool OfficeComplete,
    IReadOnlyList<OwnSpecializationSnapshot> Specializations,
    int DocumentCount,
    IReadOnlyList<string> ActiveDocumentTypes,
    byte[] RowVersion);

internal sealed record OwnOfficeSnapshot(
    Guid Id,
    int GovernorateId,
    string GovernorateNameAr,
    string GovernorateNameEn,
    int CityId,
    string CityNameAr,
    string CityNameEn,
    int AreaId,
    string AreaNameAr,
    string AreaNameEn,
    string DetailedAddress,
    string? PublicPhoneNumber,
    byte[] RowVersion);

internal sealed record OwnSpecializationSnapshot(int Id, string NameAr, string NameEn, bool IsActive);
