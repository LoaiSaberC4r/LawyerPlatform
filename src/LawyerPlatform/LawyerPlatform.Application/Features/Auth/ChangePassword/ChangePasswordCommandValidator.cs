using BuildingBlock.Application.Abstraction.Encryption;
using FluentValidation;

namespace LawyerPlatform.Application.Features.Auth.ChangePassword;

internal sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator(IPasswordService passwordService)
    {
        RuleFor(command => command.CurrentPassword)
            .NotEmpty().WithErrorCode("Account.CurrentPasswordRequired")
            .MaximumLength(1024).WithErrorCode("Account.CurrentPasswordInvalid");

        RuleFor(command => command.NewPassword)
            .NotEmpty().WithErrorCode("Account.NewPasswordRequired")
            .Must(passwordService.IsStrongPassword).WithErrorCode("Account.NewPasswordInvalid");
    }
}
