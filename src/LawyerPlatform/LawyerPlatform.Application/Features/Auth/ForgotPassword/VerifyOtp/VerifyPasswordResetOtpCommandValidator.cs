using FluentValidation;

namespace LawyerPlatform.Application.Features.Auth.ForgotPassword.VerifyOtp;

internal sealed class VerifyPasswordResetOtpCommandValidator
    : AbstractValidator<VerifyPasswordResetOtpCommand>
{
    public VerifyPasswordResetOtpCommandValidator()
    {
        RuleFor(command => command.RequestId)
            .NotEmpty().WithErrorCode("PasswordReset.RequestIdRequired");

        RuleFor(command => command.Otp)
            .NotEmpty().WithErrorCode("PasswordReset.OtpRequired")
            .Length(6).WithErrorCode("PasswordReset.OtpInvalid")
            .Matches("^[0-9]{6}$").WithErrorCode("PasswordReset.OtpInvalid");
    }
}
