using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Application.Abstractions.Authentication;

public sealed record PasswordLifecycleState(
    DateTime PasswordExpiresOnUtc,
    bool PasswordExpired,
    bool PasswordChangeRequired,
    PasswordChangeReason PasswordChangeReason);

public interface IPasswordLifecycleService
{
    PasswordLifecycleState Evaluate(UserAccount account);
}
