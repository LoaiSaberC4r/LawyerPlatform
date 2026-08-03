using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.PublicLawyers.Common;

internal abstract class PublicLawyerSpecification<TResponse> : Specification<LawyerProfile, TResponse>
{
    protected void ApplyPublicEligibility(ILawyerDocumentPolicy documentPolicy)
    {
        AddCriteria(profile =>
            profile.ApprovalStatus == LawyerApprovalStatus.Approved &&
            profile.UserAccount.Status == AccountStatus.Active &&
            !profile.IsDeleted &&
            profile.FullName != "" &&
            profile.ProfessionalTitle != null && profile.ProfessionalTitle != "" &&
            profile.YearsOfExperience != null && profile.YearsOfExperience >= 0 &&
            profile.ProfessionalRegistrationNumber != null && profile.ProfessionalRegistrationNumber != "" &&
            profile.Offices.Any(office =>
                office.IsPrimary && office.IsActive &&
                office.Governorate.IsActive && office.City.IsActive && office.Area.IsActive &&
                office.City.GovernorateId == office.GovernorateId && office.Area.CityId == office.CityId &&
                office.DetailedAddress != "") &&
            profile.Specializations.Any() &&
            !profile.Specializations.Any(item => !item.LegalSpecialization.IsActive));

        if (documentPolicy.RequiredDocumentTypes.Count == 0)
        {
            AddCriteria(_ => false);
            return;
        }

        foreach (var requiredDocumentType in documentPolicy.RequiredDocumentTypes)
        {
            var documentType = requiredDocumentType;
            AddCriteria(profile => profile.Documents.Any(document =>
                !document.IsDeleted && document.DocumentType == documentType));
        }
    }
}

public sealed record PublicLawyerResponse(
    Guid Id,
    string FullName,
    string? ProfileImageUrl,
    string ProfessionalTitle,
    string? Biography,
    int YearsOfExperience,
    IReadOnlyList<LawyerSpecializationResponse> Specializations,
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
    string? PublicPhoneNumber);

internal sealed record PublicLawyerSnapshot(
    Guid Id,
    string FullName,
    bool HasProfileImage,
    string ProfessionalTitle,
    string? Biography,
    int YearsOfExperience,
    IReadOnlyList<PublicSpecializationSnapshot> Specializations,
    PublicOfficeSnapshot Office)
{
    public PublicLawyerResponse ToResponse() => new(
        Id,
        FullName,
        HasProfileImage ? $"/api/v1/public/lawyers/{Id}/profile-image" : null,
        ProfessionalTitle,
        Biography,
        YearsOfExperience,
        Specializations.Select(item => new LawyerSpecializationResponse(item.Id, item.NameAr, item.NameEn)).ToArray(),
        Office.GovernorateId,
        Office.GovernorateNameAr,
        Office.GovernorateNameEn,
        Office.CityId,
        Office.CityNameAr,
        Office.CityNameEn,
        Office.AreaId,
        Office.AreaNameAr,
        Office.AreaNameEn,
        Office.DetailedAddress,
        Office.PublicPhoneNumber);
}

internal sealed record PublicSpecializationSnapshot(int Id, string NameAr, string NameEn);

internal sealed record PublicOfficeSnapshot(
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
    string? PublicPhoneNumber);

internal static class PublicLawyerProjection
{
    public static System.Linq.Expressions.Expression<Func<LawyerProfile, PublicLawyerSnapshot>> Create()
        => profile => new PublicLawyerSnapshot(
            profile.Id,
            profile.FullName,
            profile.ProfileImageStorageKey != null,
            profile.ProfessionalTitle!,
            profile.Biography,
            profile.YearsOfExperience!.Value,
            profile.Specializations.Where(item => item.LegalSpecialization.IsActive)
                .OrderBy(item => item.LegalSpecialization.DisplayOrder)
                .ThenBy(item => item.LegalSpecializationId)
                .Select(item => new PublicSpecializationSnapshot(
                    item.LegalSpecializationId,
                    item.LegalSpecialization.NameAr,
                    item.LegalSpecialization.NameEn)).ToArray(),
            profile.Offices.Where(office => office.IsPrimary && office.IsActive)
                .Select(office => new PublicOfficeSnapshot(
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
                    office.PublicPhoneNumber)).Single());
}
