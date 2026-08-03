namespace LawyerPlatform.Application.Features.Auth.Common;

public sealed record AuthTokenResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresOnUtc,
    string Role,
    bool IsFirstLogin,
    DateTime PasswordChangedOnUtc,
    DateTime PasswordExpiresOnUtc,
    bool PasswordExpired,
    bool PasswordChangeRequired,
    string PasswordChangeReason);
