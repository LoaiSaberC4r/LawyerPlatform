using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.ReferenceData;

public static class LegalSpecializationErrors
{
    public static readonly Error Invalid = Error.Validation(
        "LegalSpecialization.Invalid",
        "The legal specialization is invalid.");

    public static readonly Error NotFound = Error.NotFound(
        "LegalSpecialization.NotFound",
        "The legal specialization was not found.");

    public static readonly Error DuplicateNameAr = Error.Conflict(
        "LegalSpecialization.DuplicateNameAr",
        "A legal specialization with the same Arabic name already exists.");

    public static readonly Error DuplicateNameEn = Error.Conflict(
        "LegalSpecialization.DuplicateNameEn",
        "A legal specialization with the same English name already exists.");

    public static readonly Error AlreadyActive = Error.Conflict(
        "LegalSpecialization.AlreadyActive",
        "The legal specialization is already active.");

    public static readonly Error AlreadyInactive = Error.Conflict(
        "LegalSpecialization.AlreadyInactive",
        "The legal specialization is already inactive.");

    public static readonly Error InvalidRowVersion = Error.Validation(
        "LegalSpecialization.InvalidRowVersion",
        "RowVersion must be a valid Base64 value.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "LegalSpecialization.ConcurrencyConflict",
        "The legal specialization was changed by another request.");
}
