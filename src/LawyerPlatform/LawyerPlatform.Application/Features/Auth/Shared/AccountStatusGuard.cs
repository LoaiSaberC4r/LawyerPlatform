using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Application.Features.Auth.Common;

internal static class AccountStatusGuard
{
    public static Result EnsureActive(UserAccount account)
        => account.Status switch
        {
            AccountStatus.Active => Result.Ok(),
            AccountStatus.Suspended => Result.Fail(AccountErrors.Suspended),
            AccountStatus.Inactive => Result.Fail(AccountErrors.Inactive),
            _ => Result.Fail(AccountErrors.Inactive)
        };
}
