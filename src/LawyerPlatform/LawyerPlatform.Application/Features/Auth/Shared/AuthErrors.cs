using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Application.Features.Auth.Common;

public static class AuthErrors
{
    public static Error InvalidCredentials => Error.Unauthorized("Auth.Login.InvalidCredentials", ErrorMessage.InvalidLogin);
    public static Error Unauthenticated => Error.Unauthorized("Auth.ChangePassword.Unauthenticated", ErrorMessage.AuthenticationRequired);
    public static Error PasswordHashingFailed => Error.Infra("Auth.PasswordHashingFailed", ErrorMessage.PasswordCouldNotBeProcessed);
}
