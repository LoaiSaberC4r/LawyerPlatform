using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.Lawyers;

public static class LawyerErrors
{
    public static readonly Error NotFound = Error.NotFound("Lawyer.NotFound", "Lawyer was not found.");
    public static readonly Error ProfileIncomplete = Error.Domain("Lawyer.ProfileIncomplete", "The lawyer profile is incomplete.");
    public static readonly Error InvalidApprovalStatus = Error.Domain("Lawyer.InvalidApprovalStatus", "The approval status does not allow this operation.");
    public static readonly Error ProfileEditNotAllowed = Error.Domain("Lawyer.ProfileEditNotAllowed", "The lawyer profile cannot be edited in its current status.");
    public static readonly Error SensitiveProfileEditNotAllowed = Error.Domain("Lawyer.SensitiveProfileEditNotAllowed", "Approved lawyer identity fields cannot be edited.");
    public static readonly Error NotEligibleForPublicDisplay = Error.NotFound("Lawyer.NotFound", "Lawyer was not found.");
    public static readonly Error ApprovalReasonRequired = Error.Validation("Lawyer.ApprovalReasonRequired", "An approval reason is required.");
    public static readonly Error SuspensionReasonRequired = Error.Validation("Lawyer.SuspensionReasonRequired", "A suspension reason is required.");
    public static readonly Error RegistrationNumberAlreadyExists = Error.Conflict("Lawyer.ProfessionalRegistrationNumberAlreadyExists", "The professional registration number is already in use.");
    public static readonly Error OfficeNotFound = Error.NotFound("Lawyer.OfficeNotFound", "The primary office was not found.");
    public static readonly Error OfficeEditNotAllowed = Error.Domain("Lawyer.OfficeEditNotAllowed", "The primary office cannot be edited in the current status.");
    public static readonly Error InvalidLatitude = Error.Validation("Lawyer.InvalidLatitude", "Latitude must be between -90 and 90.");
    public static readonly Error InvalidLongitude = Error.Validation("Lawyer.InvalidLongitude", "Longitude must be between -180 and 180.");
    public static readonly Error InvalidOfficeCoordinates = Error.Validation("Lawyer.InvalidOfficeCoordinates", "Latitude and longitude must both be supplied or both be null.");
    public static readonly Error DuplicateSpecialization = Error.Validation("Lawyer.DuplicateSpecialization", "Duplicate specialization identifiers are not allowed.");
    public static readonly Error DocumentNotFound = Error.NotFound("Lawyer.DocumentNotFound", "Document was not found.");
    public static readonly Error DocumentAccessDenied = Error.NotFound("Lawyer.DocumentNotFound", "Document was not found.");
    public static readonly Error DocumentTypeRequired = Error.Validation("Lawyer.DocumentTypeRequired", "Document type is required.");
    public static readonly Error InvalidDocumentType = Error.Validation("Lawyer.InvalidDocumentType", "Document type is not configured.");
    public static readonly Error InvalidDocument = Error.Validation("Lawyer.InvalidDocument", "The uploaded document is invalid.");
    public static readonly Error RequiredDocumentCannotBeRemoved = Error.Domain("Lawyer.RequiredDocumentCannotBeRemoved", "The required document cannot be removed from an approved profile.");
    public static readonly Error DocumentRequirementsNotConfigured = Error.Infra("Lawyer.DocumentRequirementsNotConfigured", "Lawyer document requirements are not configured.");
    public static readonly Error ProfileImageNotFound = Error.NotFound("Lawyer.ProfileImageNotFound", "Profile image was not found.");
    public static readonly Error InvalidRowVersion = Error.Validation("Lawyer.InvalidRowVersion", "RowVersion must be a valid Base64 value.");
    public static readonly Error ConcurrencyConflict = Error.Conflict("Lawyer.ConcurrencyConflict", "The lawyer record was changed by another request.");
    public static readonly Error ConsultationPriceInvalid = Error.Validation(
        "Lawyer.ConsultationPriceInvalid",
        "Online consultation price must be greater than zero, within the supported maximum, and contain at most two decimal places.");
    public static readonly Error AvailabilityInvalid = Error.Validation(
        "Lawyer.AvailabilityInvalid",
        "Availability must contain at most one valid working period per day and start before end.");
    public static readonly Error DuplicateAvailabilityDay = Error.Validation(
        "Lawyer.DuplicateAvailabilityDay",
        "Availability cannot contain duplicate days within the same consultation type.");
    public static readonly Error ConsultationSettingsInvalidRowVersion = Error.Validation(
        "Lawyer.ConsultationSettingsInvalidRowVersion",
        "Consultation settings RowVersion must be a valid Base64 value.");
    public static readonly Error ConsultationSettingsConcurrencyConflict = Error.Conflict(
        "Lawyer.ConsultationSettingsConcurrencyConflict",
        "The consultation settings were changed by another request.");
}
