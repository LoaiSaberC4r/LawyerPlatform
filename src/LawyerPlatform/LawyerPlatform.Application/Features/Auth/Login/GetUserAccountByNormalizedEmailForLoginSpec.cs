using BuildingBlock.Domain.Specification;
using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Application.Features.Auth.Login;

internal sealed class GetUserAccountByNormalizedEmailForLoginSpec : Specification<UserAccount>
{
    public GetUserAccountByNormalizedEmailForLoginSpec(string normalizedEmail)
    {
        AddCriteria(account => account.NormalizedEmail == normalizedEmail);
        UseNoTracking();
    }
}
