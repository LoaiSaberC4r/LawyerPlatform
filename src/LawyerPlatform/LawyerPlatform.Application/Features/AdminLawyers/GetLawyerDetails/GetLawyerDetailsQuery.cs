using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Abstractions.Media;
using LawyerPlatform.Application.Features.AdminLawyers.GetLawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.AdminLawyers.GetLawyerDetails;

public sealed record GetLawyerDetailsQuery(Guid LawyerId) : IQuery<AdminLawyerDetailsResponse>;

public sealed record AdminAccountSummaryResponse(
    Guid Id,
    string UserName,
    string Email,
    string PhoneNumber,
    string Status);

public sealed record AdminProfessionalProfileResponse(
    string FullName,
    string? ProfessionalTitle,
    string? Biography,
    int? YearsOfExperience,
    string? ProfessionalRegistrationNumber,
    bool HasProfileImage,
    string? ProfileImagePath);

public sealed record LawyerApprovalHistoryResponse(
    Guid Id,
    string OldStatus,
    string NewStatus,
    Guid ChangedByUserId,
    string? Reason,
    DateTime ChangedOnUtc);

public sealed record AdminLawyerDetailsResponse(
    Guid Id,
    AdminAccountSummaryResponse Account,
    AdminProfessionalProfileResponse ProfessionalProfile,
    LawyerOfficeResponse? PrimaryOffice,
    IReadOnlyList<LawyerSpecializationResponse> Specializations,
    IReadOnlyList<LawyerDocumentResponse> Documents,
    LawyerProfileCompletionResponse Completion,
    string ApprovalStatus,
    string? ApprovalReason,
    DateTime? SubmittedOnUtc,
    DateTime? ApprovedOnUtc,
    DateTime? SuspendedOnUtc,
    string? SuspensionReason,
    IReadOnlyList<LawyerApprovalHistoryResponse> StatusHistory,
    string RowVersion);

internal sealed class GetLawyerDetailsQueryHandler(
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> repository,
    ILawyerDocumentPolicy documentPolicy,
    IStoredFileAvailability storedFileAvailability,
    IProfileImagePathResolver profileImagePathResolver)
    : IQueryHandler<GetLawyerDetailsQuery, AdminLawyerDetailsResponse>
{
    public async Task<Result<AdminLawyerDetailsResponse>> Handle(GetLawyerDetailsQuery query, CancellationToken cancellationToken)
    {
        var item = await repository.FirstOrDefaultAsync(new AdminLawyerDetailsSpecification(query.LawyerId), cancellationToken);
        if (item is null)
        {
            return Result<AdminLawyerDetailsResponse>.Fail(LawyerErrors.NotFound);
        }

        var availableDocumentTypes = await RequiredDocumentAvailability.GetAvailableTypesAsync(
            item.Documents
                .Select(document => new StoredDocumentReference(document.DocumentType, document.StorageKey))
                .ToArray(),
            documentPolicy,
            storedFileAvailability,
            cancellationToken);
        var completion = LawyerProfileCompletionCalculator.Calculate(
            item.ApprovalStatus,
            item.FullName,
            item.ProfessionalTitle,
            item.YearsOfExperience,
            item.ProfessionalRegistrationNumber,
            item.OfficeComplete,
            item.Specializations.Any(specialization => specialization.IsActive),
            availableDocumentTypes,
            item.AccountStatus == AccountStatus.Active,
            documentPolicy);
        var profileImagePath = profileImagePathResolver.Resolve(item.ProfileImageStorageKey);

        return Result<AdminLawyerDetailsResponse>.Ok(new AdminLawyerDetailsResponse(
            item.Id,
            new AdminAccountSummaryResponse(item.UserAccountId, item.UserName, item.Email, item.PhoneNumber, item.AccountStatus.ToString()),
            new AdminProfessionalProfileResponse(
                item.FullName,
                item.ProfessionalTitle,
                item.Biography,
                item.YearsOfExperience,
                item.ProfessionalRegistrationNumber,
                profileImagePath is not null,
                profileImagePath),
            item.Office?.ToResponse(),
            item.Specializations.Select(specialization => new LawyerSpecializationResponse(
                specialization.Id,
                specialization.NameAr,
                specialization.NameEn)).ToArray(),
            item.Documents.Select(document => new LawyerDocumentResponse(
                document.Id,
                document.DocumentType,
                document.OriginalFileName,
                document.ContentType,
                document.FileSize,
                document.UploadedOnUtc,
                $"/api/v1/admin/lawyers/{item.Id}/documents/{document.Id}/content",
                RowVersionCodec.Encode(document.RowVersion))).ToArray(),
            completion,
            item.ApprovalStatus.ToString(),
            item.ApprovalReason,
            item.SubmittedOnUtc,
            item.ApprovedOnUtc,
            item.SuspendedOnUtc,
            item.SuspensionReason,
            item.History.Select(history => new LawyerApprovalHistoryResponse(
                history.Id,
                history.OldStatus.ToString(),
                history.NewStatus.ToString(),
                history.ChangedByUserId,
                history.Reason,
                history.ChangedOnUtc)).ToArray(),
            RowVersionCodec.Encode(item.RowVersion)));
    }
}

internal sealed class AdminLawyerDetailsSpecification : Specification<LawyerProfile, AdminLawyerDetailsSnapshot>
{
    public AdminLawyerDetailsSpecification(Guid lawyerId)
    {
        AddCriteria(profile => profile.Id == lawyerId);
        UseNoTracking();
        UseSplitQuery();
        Select(profile => new AdminLawyerDetailsSnapshot(
            profile.Id,
            profile.UserAccountId,
            profile.UserAccount.UserName,
            profile.UserAccount.Email,
            profile.UserAccount.PhoneNumber,
            profile.UserAccount.Status,
            profile.FullName,
            profile.ProfessionalTitle,
            profile.Biography,
            profile.YearsOfExperience,
            profile.ProfessionalRegistrationNumber,
            profile.ProfileImageStorageKey,
            profile.Offices.Where(office => office.IsPrimary && office.IsActive)
                .Select(office => new AdminOfficeDetailsSnapshot(
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
                    office.Latitude,
                    office.Longitude,
                    office.RowVersion)).FirstOrDefault(),
            profile.Offices.Any(office =>
                office.IsPrimary && office.IsActive && office.Governorate.IsActive && office.City.IsActive && office.Area.IsActive &&
                office.City.GovernorateId == office.GovernorateId && office.Area.CityId == office.CityId && office.DetailedAddress != ""),
            profile.Specializations.OrderBy(item => item.LegalSpecialization.DisplayOrder)
                .Select(item => new AdminSpecializationSnapshot(
                    item.LegalSpecializationId,
                    item.LegalSpecialization.NameAr,
                    item.LegalSpecialization.NameEn,
                    item.LegalSpecialization.IsActive)).ToArray(),
            profile.Documents.Where(document => !document.IsDeleted)
                .OrderByDescending(document => document.UploadedOnUtc)
                .Select(document => new AdminDocumentSnapshot(
                    document.Id,
                    document.DocumentType,
                    document.StorageKey,
                    document.OriginalFileName,
                    document.ContentType,
                    document.FileSize,
                    document.UploadedOnUtc,
                    document.RowVersion)).ToArray(),
            profile.ApprovalStatus,
            profile.ApprovalReason,
            profile.SubmittedOnUtc,
            profile.ApprovedOnUtc,
            profile.SuspendedOnUtc,
            profile.SuspensionReason,
            profile.StatusHistory.OrderByDescending(history => history.ChangedOnUtc)
                .ThenByDescending(history => history.Id)
                .Select(history => new AdminHistorySnapshot(
                    history.Id,
                    history.OldStatus,
                    history.NewStatus,
                    history.ChangedByUserId,
                    history.Reason,
                    history.ChangedOnUtc)).ToArray(),
            profile.RowVersion));
    }
}

internal sealed record AdminLawyerDetailsSnapshot(
    Guid Id,
    Guid UserAccountId,
    string UserName,
    string Email,
    string PhoneNumber,
    AccountStatus AccountStatus,
    string FullName,
    string? ProfessionalTitle,
    string? Biography,
    int? YearsOfExperience,
    string? ProfessionalRegistrationNumber,
    string? ProfileImageStorageKey,
    AdminOfficeDetailsSnapshot? Office,
    bool OfficeComplete,
    IReadOnlyList<AdminSpecializationSnapshot> Specializations,
    IReadOnlyList<AdminDocumentSnapshot> Documents,
    LawyerApprovalStatus ApprovalStatus,
    string? ApprovalReason,
    DateTime? SubmittedOnUtc,
    DateTime? ApprovedOnUtc,
    DateTime? SuspendedOnUtc,
    string? SuspensionReason,
    IReadOnlyList<AdminHistorySnapshot> History,
    byte[] RowVersion);

internal sealed record AdminOfficeDetailsSnapshot(
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
    decimal? Latitude,
    decimal? Longitude,
    byte[] RowVersion)
{
    public LawyerOfficeResponse ToResponse() => new(
        Id, GovernorateId, GovernorateNameAr, GovernorateNameEn,
        CityId, CityNameAr, CityNameEn,
        AreaId, AreaNameAr, AreaNameEn,
        DetailedAddress, PublicPhoneNumber, Latitude, Longitude, RowVersionCodec.Encode(RowVersion));
}

internal sealed record AdminDocumentSnapshot(
    Guid Id,
    string DocumentType,
    string StorageKey,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    DateTime UploadedOnUtc,
    byte[] RowVersion);

internal sealed record AdminHistorySnapshot(
    Guid Id,
    LawyerApprovalStatus OldStatus,
    LawyerApprovalStatus NewStatus,
    Guid ChangedByUserId,
    string? Reason,
    DateTime ChangedOnUtc);
