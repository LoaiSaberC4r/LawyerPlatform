using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Application.Abstractions.Authentication;

public sealed record JwtToken(string AccessToken, DateTime ExpiresOnUtc);

public interface IJwtProvider
{
    JwtToken GenerateToken(UserAccount account, PasswordLifecycleState passwordLifecycle);
}
