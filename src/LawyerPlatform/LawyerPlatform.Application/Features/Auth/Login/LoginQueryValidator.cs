using FluentValidation;

namespace LawyerPlatform.Application.Features.Auth.Login;

internal sealed class LoginQueryValidator : AbstractValidator<LoginQuery>
{
    public LoginQueryValidator()
    {
        RuleFor(query => query.UserNameOrEmail)
            .NotEmpty().WithErrorCode("Auth.Login.IdentifierRequired")
            .MaximumLength(200).WithErrorCode("Auth.Login.IdentifierInvalid");

        RuleFor(query => query.Password)
            .NotEmpty().WithErrorCode("Account.PasswordRequired")
            .MaximumLength(1024).WithErrorCode("Auth.Login.InvalidCredentials");
    }
}
