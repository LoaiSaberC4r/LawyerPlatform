namespace LawyerPlatform.Infrastructure.Options;

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public int OtpLength { get; set; } = 6;
    public int OtpExpirationMinutes { get; set; } = 5;
    public int MaximumVerificationAttempts { get; set; } = 5;
    public int ResendCooldownSeconds { get; set; } = 60;
    public int ResetTokenExpirationMinutes { get; set; } = 10;
    public string HmacSecret { get; set; } = string.Empty;
}
