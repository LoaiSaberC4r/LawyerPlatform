using BuildingBlock.Application.Abstraction.Encryption;
using FluentValidation;
using LawyerPlatform.Application.Features.Auth.Common;

namespace LawyerPlatform.Application.Features.Auth.RegisterLawyer;

internal sealed class RegisterLawyerCommandValidator : AbstractValidator<RegisterLawyerCommand>
{
    public RegisterLawyerCommandValidator(IPasswordService passwordService)
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
            .NotEmpty().WithErrorCode("Account.PhoneNumberRequired")
            .MaximumLength(30).WithErrorCode("Account.PhoneNumberInvalid");

        RuleFor(command => command.Password)
            .NotEmpty().WithErrorCode("Account.PasswordRequired")
            .Must(passwordService.IsStrongPassword).WithErrorCode("Account.PasswordInvalid");
    }
}
