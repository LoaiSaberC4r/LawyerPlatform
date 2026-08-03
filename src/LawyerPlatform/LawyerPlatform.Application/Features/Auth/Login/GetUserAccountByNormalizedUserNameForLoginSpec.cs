using BuildingBlock.Domain.Specification;
using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Application.Features.Auth.Login;

internal sealed class GetUserAccountByNormalizedUserNameForLoginSpec : Specification<UserAccount>
{
    public GetUserAccountByNormalizedUserNameForLoginSpec(string normalizedUserName)
    {
        AddCriteria(account => account.NormalizedUserName == normalizedUserName);
        UseNoTracking();
    }
}
