using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Domain.Accounts;

public static class PasswordResetErrors
{
    public static Error OtpInvalidOrExpired => Error.Validation(
        "PasswordReset.OtpInvalidOrExpired",
        ErrorMessage.OtpInvalidOrExpired);

    public static Error ResetTokenInvalidOrExpired => Error.Validation(
        "PasswordReset.ResetTokenInvalidOrExpired",
        ErrorMessage.ResetTokenInvalidOrExpired);

    public static Error PasswordMustBeDifferent => Error.Validation(
        "PasswordReset.PasswordMustBeDifferent",
        ErrorMessage.PasswordMustBeDifferent);

    public static Error ChallengeConsumed => Error.Validation(
        "PasswordReset.ChallengeConsumed",
        ErrorMessage.PasswordResetChallengeConsumed);

    public static Error ConcurrencyConflict => Error.Conflict(
        "PasswordReset.ConcurrencyConflict",
        ErrorMessage.PasswordResetConcurrencyConflict);

    public static Error ConfigurationInvalid => Error.Domain(
        "PasswordReset.ConfigurationInvalid",
        ErrorMessage.PasswordResetConfigurationInvalid);

    public static Error RateLimitExceeded(TimeSpan retryAfter) => Error.RateLimit(
        "PasswordReset.RateLimitExceeded",
        ErrorMessage.PasswordRecoveryRateLimitExceeded,
        retryAfter);
}
