using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Domain.Accounts;

public static class AccountErrors
{
    public static Error UserNameRequired => Error.Validation("Account.UserNameRequired", ErrorMessage.UserNameRequired);
    public static Error UserNameInvalid => Error.Validation("Account.UserNameInvalid", ErrorMessage.UserNameInvalid);
    public static Error UserNameTooShort => Error.Validation("Account.UserNameTooShort", ErrorMessage.UserNameTooShort);
    public static Error UserNameTooLong => Error.Validation("Account.UserNameTooLong", ErrorMessage.UserNameTooLong);
    public static Error UserNameAlreadyExists => Error.Conflict("Account.UserNameAlreadyExists", ErrorMessage.UserNameAlreadyExists);
    public static Error EmailRequired => Error.Validation("Account.EmailRequired", ErrorMessage.EmailRequired);
    public static Error EmailInvalid => Error.Validation("Account.EmailInvalid", ErrorMessage.EmailInvalid);
    public static Error EmailAlreadyExists => Error.Conflict("Account.EmailAlreadyExists", ErrorMessage.EmailAlreadyExists);
    public static Error PhoneNumberRequired => Error.Validation("Account.PhoneNumberRequired", ErrorMessage.PhoneNumberRequired);
    public static Error PhoneNumberAlreadyExists => Error.Conflict("Account.PhoneNumberAlreadyExists", ErrorMessage.PhoneNumberAlreadyExists);
    public static Error PasswordRequired => Error.Validation("Account.PasswordRequired", ErrorMessage.PasswordRequired);
    public static Error PasswordInvalid => Error.Validation("Account.PasswordInvalid", ErrorMessage.PasswordInvalid);
    public static Error NewPasswordRequired => Error.Validation("Account.NewPasswordRequired", ErrorMessage.NewPasswordRequired);
    public static Error NewPasswordInvalid => Error.Validation("Account.NewPasswordInvalid", ErrorMessage.NewPasswordInvalid);
    public static Error FullNameRequired => Error.Validation("Account.FullNameRequired", ErrorMessage.FullNameRequired);
    public static Error NotFound => Error.NotFound("Account.NotFound", ErrorMessage.AccountNotFound);
    public static Error Suspended => Error.Security("Account.Suspended", ErrorMessage.AccountSuspended);
    public static Error Inactive => Error.Security("Account.Inactive", ErrorMessage.AccountInactive);
    public static Error CurrentPasswordInvalid => Error.Unauthorized("Account.CurrentPasswordInvalid", ErrorMessage.CurrentPasswordInvalid);
    public static Error PasswordMustBeDifferent => Error.Validation("Account.PasswordMustBeDifferent", ErrorMessage.PasswordMustBeDifferent);
    public static Error PasswordChangeRequired => Error.Security("Account.PasswordChangeRequired", ErrorMessage.PasswordChangeRequired);
    public static Error PasswordHashRequired => Error.Domain("Account.PasswordHashRequired", ErrorMessage.PasswordHashRequired);
    public static Error FullNameTooLong => Error.Validation("Account.FullNameTooLong", ErrorMessage.FullNameTooLong);
    public static Error InvalidStatusTransition => Error.Domain("Account.InvalidStatusTransition", ErrorMessage.InvalidStatusTransition);
    public static Error InvalidRowVersion => Error.Validation("Account.InvalidRowVersion", ErrorMessage.InvalidRowVersion);
    public static Error ConcurrencyConflict => Error.Conflict("Account.ConcurrencyConflict", ErrorMessage.AccountConcurrencyConflict);
    public static Error CredentialVersionLimitReached => Error.Conflict("Account.CredentialVersionLimitReached", ErrorMessage.CredentialVersionLimitReached);
}
