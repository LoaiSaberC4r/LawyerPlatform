using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Domain.ReferenceData;

public static class LegalSpecializationErrors
{
    public static Error Invalid => Error.Validation(
        "LegalSpecialization.Invalid",
        ErrorMessage.LegalSpecializationInvalid);

    public static Error NotFound => Error.NotFound(
        "LegalSpecialization.NotFound",
        ErrorMessage.LegalSpecializationNotFound);

    public static Error DuplicateNameAr => Error.Conflict(
        "LegalSpecialization.DuplicateNameAr",
        ErrorMessage.LegalSpecializationDuplicateNameAr);

    public static Error DuplicateNameEn => Error.Conflict(
        "LegalSpecialization.DuplicateNameEn",
        ErrorMessage.LegalSpecializationDuplicateNameEn);

    public static Error AlreadyActive => Error.Conflict(
        "LegalSpecialization.AlreadyActive",
        ErrorMessage.LegalSpecializationAlreadyActive);

    public static Error AlreadyInactive => Error.Conflict(
        "LegalSpecialization.AlreadyInactive",
        ErrorMessage.LegalSpecializationAlreadyInactive);

    public static Error InvalidRowVersion => Error.Validation(
        "LegalSpecialization.InvalidRowVersion",
        ErrorMessage.InvalidRowVersion);

    public static Error ConcurrencyConflict => Error.Conflict(
        "LegalSpecialization.ConcurrencyConflict",
        ErrorMessage.LegalSpecializationConcurrencyConflict);
}
