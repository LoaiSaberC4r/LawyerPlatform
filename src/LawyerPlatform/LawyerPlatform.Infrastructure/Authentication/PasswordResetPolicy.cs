using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Authentication;

internal sealed class PasswordResetPolicy(IOptions<PasswordResetOptions> options)
    : IPasswordResetPolicy
{
    private PasswordResetOptions Value => options.Value;

    public int OtpLength => Value.OtpLength;
    public int OtpExpirationMinutes => Value.OtpExpirationMinutes;
    public int MaximumVerificationAttempts => Value.MaximumVerificationAttempts;
    public int ResendCooldownSeconds => Value.ResendCooldownSeconds;
    public int ResetTokenExpirationMinutes => Value.ResetTokenExpirationMinutes;
}
