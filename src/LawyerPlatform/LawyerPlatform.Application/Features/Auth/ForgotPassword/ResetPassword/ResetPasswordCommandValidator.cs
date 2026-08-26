using FluentValidation;

namespace LawyerPlatform.Application.Features.Auth.ForgotPassword.ResetPassword;

internal sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.RequestId)
            .NotEmpty().WithErrorCode("PasswordReset.RequestIdRequired");

        RuleFor(command => command.ResetToken)
            .NotEmpty().WithErrorCode("PasswordReset.ResetTokenRequired")
            .MaximumLength(1024).WithErrorCode("PasswordReset.ResetTokenInvalid");

        RuleFor(command => command.NewPassword)
            .NotEmpty().WithErrorCode("Account.NewPasswordRequired")
            .MaximumLength(1024).WithErrorCode("Account.NewPasswordInvalid");

        RuleFor(command => command.ConfirmPassword)
            .NotEmpty().WithErrorCode("PasswordReset.ConfirmPasswordRequired")
            .Equal(command => command.NewPassword)
            .WithErrorCode("PasswordReset.PasswordConfirmationMismatch");
    }
}
