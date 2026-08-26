using FluentValidation;

namespace LawyerPlatform.Application.Features.Auth.ForgotPassword.RequestOtp;

internal sealed class RequestPasswordResetOtpCommandValidator
    : AbstractValidator<RequestPasswordResetOtpCommand>
{
    public RequestPasswordResetOtpCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithErrorCode("PasswordReset.EmailRequired")
            .MaximumLength(200).WithErrorCode("PasswordReset.EmailInvalid")
            .EmailAddress().WithErrorCode("PasswordReset.EmailInvalid");
    }
}
