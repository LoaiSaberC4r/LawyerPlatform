using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Application.Features.Auth.Common;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = Error.Unauthorized("Auth.Login.InvalidCredentials", "Invalid credentials.");
    public static readonly Error Unauthenticated = Error.Unauthorized("Auth.ChangePassword.Unauthenticated", "Authentication is required.");
    public static readonly Error PasswordHashingFailed = Error.Infra("Auth.PasswordHashingFailed", "The password could not be processed.");
}
