using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Application.Features.Lawyers.Common;

internal static class LawyerApplicationErrors
{
    public static Error AccountNotFound => Error.NotFound("Account.NotFound", ErrorMessage.AccountNotFound);
    public static Error AccountInactive => Error.Domain("Account.Inactive", ErrorMessage.AccountInactive);
    public static Error AccountSuspended => Error.Domain("Account.Suspended", ErrorMessage.AccountSuspended);
    public static Error LocationNotFound => Error.NotFound("Location.NotFound", ErrorMessage.ActiveLocationNotFound);
    public static Error InvalidLocationHierarchy => Error.Validation("Location.InvalidHierarchy", ErrorMessage.InvalidLocationHierarchy);
    public static Error SpecializationNotFound => Error.NotFound("LegalSpecialization.NotFound", ErrorMessage.LegalSpecializationNotFound);
    public static Error SpecializationInactive => Error.Domain("LegalSpecialization.Inactive", ErrorMessage.LegalSpecializationInactive);
}
