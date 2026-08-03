using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Application.Features.Auth.Common;

internal static class AuthResponseFactory
{
    public static AuthTokenResponse Create(UserAccount account, PasswordLifecycleState lifecycle, JwtToken token)
        => new(
            token.AccessToken,
            "Bearer",
            token.ExpiresOnUtc,
            account.Role.ToString(),
            account.IsFirstLogin,
            account.PasswordChangedOnUtc,
            lifecycle.PasswordExpiresOnUtc,
            lifecycle.PasswordExpired,
            lifecycle.PasswordChangeRequired,
            lifecycle.PasswordChangeReason.ToString());
}
