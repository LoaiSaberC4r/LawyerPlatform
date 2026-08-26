namespace LawyerPlatform.Application.Abstractions.Authentication;

public interface IPasswordResetPolicy
{
    int OtpLength { get; }
    int OtpExpirationMinutes { get; }
    int MaximumVerificationAttempts { get; }
    int ResendCooldownSeconds { get; }
    int ResetTokenExpirationMinutes { get; }
}
