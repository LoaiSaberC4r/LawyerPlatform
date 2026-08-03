using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.Accounts;

public static class AccountErrors
{
    public static readonly Error UserNameRequired = Error.Validation("Account.UserNameRequired", "User name is required.");
    public static readonly Error UserNameInvalid = Error.Validation("Account.UserNameInvalid", "User name format is invalid.");
    public static readonly Error UserNameTooShort = Error.Validation("Account.UserNameTooShort", "User name must be at least 3 characters.");
    public static readonly Error UserNameTooLong = Error.Validation("Account.UserNameTooLong", "User name cannot exceed 50 characters.");
    public static readonly Error UserNameAlreadyExists = Error.Conflict("Account.UserNameAlreadyExists", "User name is already in use.");
    public static readonly Error EmailRequired = Error.Validation("Account.EmailRequired", "Email is required.");
    public static readonly Error EmailInvalid = Error.Validation("Account.EmailInvalid", "Email format is invalid.");
    public static readonly Error EmailAlreadyExists = Error.Conflict("Account.EmailAlreadyExists", "Email is already in use.");
    public static readonly Error PhoneNumberRequired = Error.Validation("Account.PhoneNumberRequired", "Phone number is required.");
    public static readonly Error PhoneNumberAlreadyExists = Error.Conflict("Account.PhoneNumberAlreadyExists", "Phone number is already in use.");
    public static readonly Error PasswordRequired = Error.Validation("Account.PasswordRequired", "Password is required.");
    public static readonly Error PasswordInvalid = Error.Validation("Account.PasswordInvalid", "Password does not satisfy the configured policy.");
    public static readonly Error NewPasswordRequired = Error.Validation("Account.NewPasswordRequired", "New password is required.");
    public static readonly Error NewPasswordInvalid = Error.Validation("Account.NewPasswordInvalid", "New password does not satisfy the configured policy.");
    public static readonly Error FullNameRequired = Error.Validation("Account.FullNameRequired", "Full name is required.");
    public static readonly Error NotFound = Error.NotFound("Account.NotFound", "Account was not found.");
    public static readonly Error Suspended = Error.Security("Account.Suspended", "The account is suspended.");
    public static readonly Error Inactive = Error.Security("Account.Inactive", "The account is inactive.");
    public static readonly Error CurrentPasswordInvalid = Error.Unauthorized("Account.CurrentPasswordInvalid", "Current password is invalid.");
    public static readonly Error PasswordMustBeDifferent = Error.Validation("Account.PasswordMustBeDifferent", "New password must be different from the current password.");
    public static readonly Error PasswordChangeRequired = Error.Security("Account.PasswordChangeRequired", "Password change is required before accessing this resource.");
    public static readonly Error PasswordHashRequired = Error.Domain("Account.PasswordHashRequired", "Password hash is required.");
}
