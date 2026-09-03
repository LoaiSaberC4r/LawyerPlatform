using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Abstractions.Media;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;
using System.Text.Json.Serialization;

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
            profile.Specializations.Any(item => item.LegalSpecialization.IsActive));

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
    bool HasProfileImage,
    string? ProfileImagePath,
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
    string? PublicPhoneNumber,
    decimal? ConsultationPrice,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<PublicLawyerAvailabilityResponse>? Availability = null);

public sealed record PublicLawyerAvailabilityResponse(
    string DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

internal sealed record PublicLawyerSnapshot(
    Guid Id,
    string FullName,
    string? ProfileImageStorageKey,
    string ProfessionalTitle,
    string? Biography,
    int YearsOfExperience,
    IReadOnlyList<PublicSpecializationSnapshot> Specializations,
    PublicOfficeSnapshot Office,
    decimal? ConsultationPrice)
{
    public PublicLawyerResponse ToResponse(
        IProfileImagePathResolver profileImagePathResolver,
        IReadOnlyList<PublicLawyerAvailabilityResponse>? availability = null)
    {
        var profileImagePath = profileImagePathResolver.Resolve(ProfileImageStorageKey);
        return new PublicLawyerResponse(
            Id,
            FullName,
            profileImagePath is not null,
            profileImagePath,
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
            Office.PublicPhoneNumber,
            ConsultationPrice,
            availability);
    }
}

internal sealed record PublicAvailabilitySnapshot(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

internal sealed record PublicLawyerDetailsSnapshot(
    PublicLawyerSnapshot Lawyer,
    IReadOnlyList<PublicAvailabilitySnapshot> Availability)
{
    public PublicLawyerResponse ToResponse(IProfileImagePathResolver profileImagePathResolver)
        => Lawyer.ToResponse(profileImagePathResolver, Availability
            .OrderBy(item => item.DayOfWeek)
            .Select(item => new PublicLawyerAvailabilityResponse(
                item.DayOfWeek.ToString(),
                item.StartTime,
                item.EndTime))
            .ToArray());
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
            profile.ProfileImageStorageKey,
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
                    office.PublicPhoneNumber)).Single(),
            profile.ConsultationSettings == null
                ? null
                : profile.ConsultationSettings.OnlineConsultationPrice);

    public static System.Linq.Expressions.Expression<Func<LawyerProfile, PublicLawyerDetailsSnapshot>> CreateDetails()
        => profile => new PublicLawyerDetailsSnapshot(
            new PublicLawyerSnapshot(
                profile.Id,
                profile.FullName,
                profile.ProfileImageStorageKey,
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
                        office.PublicPhoneNumber)).Single(),
                profile.ConsultationSettings == null
                    ? null
                    : profile.ConsultationSettings.OnlineConsultationPrice),
            profile.ConsultationSettings == null
                ? Array.Empty<PublicAvailabilitySnapshot>()
                : profile.ConsultationSettings.Availability
                    .Where(item => item.ConsultationType == ConsultationType.Online)
                    .OrderBy(item => item.DayOfWeek)
                    .Select(item => new PublicAvailabilitySnapshot(
                        item.DayOfWeek,
                        item.StartTime,
                        item.EndTime))
                    .ToArray());
}
