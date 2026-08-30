using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Domain.Lawyers;

public static class LawyerErrors
{
    public static Error NotFound => Error.NotFound("Lawyer.NotFound", ErrorMessage.LawyerNotFound);
    public static Error ProfileIncomplete => Error.Domain("Lawyer.ProfileIncomplete", ErrorMessage.LawyerProfileIncomplete);
    public static Error InvalidApprovalStatus => Error.Domain("Lawyer.InvalidApprovalStatus", ErrorMessage.InvalidApprovalStatus);
    public static Error ProfileEditNotAllowed => Error.Domain("Lawyer.ProfileEditNotAllowed", ErrorMessage.LawyerProfileEditNotAllowed);
    public static Error SensitiveProfileEditNotAllowed => Error.Domain("Lawyer.SensitiveProfileEditNotAllowed", ErrorMessage.SensitiveProfileEditNotAllowed);
    public static Error NotEligibleForPublicDisplay => Error.NotFound("Lawyer.NotFound", ErrorMessage.LawyerNotFound);
    public static Error ApprovalReasonRequired => Error.Validation("Lawyer.ApprovalReasonRequired", ErrorMessage.ApprovalReasonRequired);
    public static Error SuspensionReasonRequired => Error.Validation("Lawyer.SuspensionReasonRequired", ErrorMessage.SuspensionReasonRequired);
    public static Error RegistrationNumberAlreadyExists => Error.Conflict("Lawyer.ProfessionalRegistrationNumberAlreadyExists", ErrorMessage.RegistrationNumberAlreadyExists);
    public static Error OfficeNotFound => Error.NotFound("Lawyer.OfficeNotFound", ErrorMessage.OfficeNotFound);
    public static Error OfficeEditNotAllowed => Error.Domain("Lawyer.OfficeEditNotAllowed", ErrorMessage.OfficeEditNotAllowed);
    public static Error InvalidLatitude => Error.Validation("Lawyer.InvalidLatitude", ErrorMessage.InvalidLatitude);
    public static Error InvalidLongitude => Error.Validation("Lawyer.InvalidLongitude", ErrorMessage.InvalidLongitude);
    public static Error InvalidOfficeCoordinates => Error.Validation("Lawyer.InvalidOfficeCoordinates", ErrorMessage.InvalidOfficeCoordinates);
    public static Error DuplicateSpecialization => Error.Validation("Lawyer.DuplicateSpecialization", ErrorMessage.DuplicateSpecialization);
    public static Error DocumentNotFound => Error.NotFound("Lawyer.DocumentNotFound", ErrorMessage.DocumentNotFound);
    public static Error DocumentAccessDenied => Error.NotFound("Lawyer.DocumentNotFound", ErrorMessage.DocumentNotFound);
    public static Error DocumentTypeRequired => Error.Validation("Lawyer.DocumentTypeRequired", ErrorMessage.DocumentTypeRequired);
    public static Error InvalidDocumentType => Error.Validation("Lawyer.InvalidDocumentType", ErrorMessage.InvalidDocumentType);
    public static Error InvalidDocument => Error.Validation("Lawyer.InvalidDocument", ErrorMessage.InvalidDocument);
    public static Error RequiredDocumentCannotBeRemoved => Error.Domain("Lawyer.RequiredDocumentCannotBeRemoved", ErrorMessage.RequiredDocumentCannotBeRemoved);
    public static Error DocumentRequirementsNotConfigured => Error.Infra("Lawyer.DocumentRequirementsNotConfigured", ErrorMessage.DocumentRequirementsNotConfigured);
    public static Error ProfileImageNotFound => Error.NotFound("Lawyer.ProfileImageNotFound", ErrorMessage.ProfileImageNotFound);
    public static Error InvalidRowVersion => Error.Validation("Lawyer.InvalidRowVersion", ErrorMessage.InvalidRowVersion);
    public static Error ConcurrencyConflict => Error.Conflict("Lawyer.ConcurrencyConflict", ErrorMessage.LawyerConcurrencyConflict);
    public static Error ConsultationPriceInvalid => Error.Validation(
        "Lawyer.ConsultationPriceInvalid",
        ErrorMessage.ConsultationPriceInvalid);
    public static Error AvailabilityInvalid => Error.Validation(
        "Lawyer.AvailabilityInvalid",
        ErrorMessage.AvailabilityInvalid);
    public static Error DuplicateAvailabilityDay => Error.Validation(
        "Lawyer.DuplicateAvailabilityDay",
        ErrorMessage.DuplicateAvailabilityDay);
    public static Error ConsultationSettingsInvalidRowVersion => Error.Validation(
        "Lawyer.ConsultationSettingsInvalidRowVersion",
        ErrorMessage.ConsultationSettingsInvalidRowVersion);
    public static Error ConsultationSettingsConcurrencyConflict => Error.Conflict(
        "Lawyer.ConsultationSettingsConcurrencyConflict",
        ErrorMessage.ConsultationSettingsConcurrencyConflict);
}
