using BuildingBlock.Application.Abstraction.Encryption;
using FluentValidation;
using LawyerPlatform.Application.Common.Validation;
using LawyerPlatform.Application.Features.Auth.Common;

namespace LawyerPlatform.Application.Features.Auth.RegisterClient;

internal sealed class RegisterClientCommandValidator : AbstractValidator<RegisterClientCommand>
{
    public RegisterClientCommandValidator(IPasswordService passwordService)
    {
        RuleFor(command => command.FullName)
            .NotEmpty().WithErrorCode("Account.FullNameRequired")
            .MaximumLength(200).WithErrorCode("Account.FullNameTooLong");

        RuleFor(command => command.UserName)
            .NotEmpty().WithErrorCode("Account.UserNameRequired")
            .MinimumLength(UserNameRules.MinimumLength).WithErrorCode("Account.UserNameTooShort")
            .MaximumLength(UserNameRules.MaximumLength).WithErrorCode("Account.UserNameTooLong")
            .Must(UserNameRules.IsValid).WithErrorCode("Account.UserNameInvalid");

        RuleFor(command => command.Email)
            .NotEmpty().WithErrorCode("Account.EmailRequired")
            .MaximumLength(200).WithErrorCode("Account.EmailInvalid")
            .EmailAddress().WithErrorCode("Account.EmailInvalid");

        RuleFor(command => command.PhoneNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("Account.PhoneNumberRequired")
            .EgyptianMobileNumber().WithErrorCode("Account.PhoneNumberInvalid");

        RuleFor(command => command.Password)
            .NotEmpty().WithErrorCode("Account.PasswordRequired")
            .Must(passwordService.IsStrongPassword).WithErrorCode("Account.PasswordInvalid");
    }
}
