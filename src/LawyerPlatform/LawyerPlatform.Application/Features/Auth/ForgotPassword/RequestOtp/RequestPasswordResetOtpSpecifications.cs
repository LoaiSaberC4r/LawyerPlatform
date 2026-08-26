using BuildingBlock.Domain.Specification;
using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Application.Features.Auth.ForgotPassword.RequestOtp;

internal sealed record PasswordResetAccountSnapshot(Guid Id, string Email);

internal sealed class PasswordResetAccountByNormalizedEmailSpec
    : Specification<UserAccount, PasswordResetAccountSnapshot>
{
    public PasswordResetAccountByNormalizedEmailSpec(string normalizedEmail)
    {
        AddCriteria(account => account.NormalizedEmail == normalizedEmail);
        UseNoTracking();
        Select(account => new PasswordResetAccountSnapshot(account.Id, account.Email));
    }
}

internal sealed class LatestPasswordResetChallengeForAccountSpec
    : Specification<PasswordResetChallenge>
{
    public LatestPasswordResetChallengeForAccountSpec(Guid userAccountId)
    {
        AddCriteria(challenge => challenge.UserAccountId == userAccountId);
        AddOrderByDescending(challenge => challenge.CreatedOnUtc);
        UseTracking();
    }
}
