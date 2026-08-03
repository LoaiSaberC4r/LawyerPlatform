using BuildingBlock.Application.Time;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Authentication;

internal sealed class PasswordLifecycleService(
    IOptions<PasswordLifecycleOptions> options,
    IDateTimeProvider clock)
    : IPasswordLifecycleService
{
    public PasswordLifecycleState Evaluate(UserAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var expiresOnUtc = account.PasswordChangedOnUtc.AddDays(options.Value.ExpiryDays);
        var expired = !account.IsFirstLogin && expiresOnUtc <= clock.UtcNow;
        var reason = account.IsFirstLogin
            ? PasswordChangeReason.FirstLogin
            : expired
                ? PasswordChangeReason.Expired
                : PasswordChangeReason.None;

        return new PasswordLifecycleState(expiresOnUtc, expired, account.IsFirstLogin || expired, reason);
    }
}
