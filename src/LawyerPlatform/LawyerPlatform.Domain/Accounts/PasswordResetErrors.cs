using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.Accounts;

public static class PasswordResetErrors
{
    public static readonly Error OtpInvalidOrExpired = Error.Validation(
        "PasswordReset.OtpInvalidOrExpired",
        "The verification code is invalid or expired.");

    public static readonly Error ResetTokenInvalidOrExpired = Error.Validation(
        "PasswordReset.ResetTokenInvalidOrExpired",
        "The password reset token is invalid or expired.");

    public static readonly Error PasswordMustBeDifferent = Error.Validation(
        "PasswordReset.PasswordMustBeDifferent",
        "New password must be different from the current password.");

    public static readonly Error ChallengeConsumed = Error.Validation(
        "PasswordReset.ChallengeConsumed",
        "The password reset request can no longer be used.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "PasswordReset.ConcurrencyConflict",
        "The password reset request was changed by another operation.");

    public static readonly Error ConfigurationInvalid = Error.Domain(
        "PasswordReset.ConfigurationInvalid",
        "Password reset is not configured correctly.");

    public static Error RateLimitExceeded(TimeSpan retryAfter) => Error.RateLimit(
        "PasswordReset.RateLimitExceeded",
        "Too many password recovery attempts.",
        retryAfter);
}
