using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Application.Features.Lawyers.Common;

internal static class LawyerApplicationErrors
{
    public static readonly Error AccountNotFound = Error.NotFound("Account.NotFound", "Account was not found.");
    public static readonly Error AccountInactive = Error.Domain("Account.Inactive", "The account is inactive.");
    public static readonly Error AccountSuspended = Error.Domain("Account.Suspended", "The account is suspended.");
    public static readonly Error LocationNotFound = Error.NotFound("Location.NotFound", "The active location was not found.");
    public static readonly Error InvalidLocationHierarchy = Error.Validation("Location.InvalidHierarchy", "The location hierarchy is invalid.");
    public static readonly Error SpecializationNotFound = Error.NotFound("LegalSpecialization.NotFound", "A requested legal specialization was not found.");
    public static readonly Error SpecializationInactive = Error.Domain("LegalSpecialization.Inactive", "A requested legal specialization is inactive.");
}
