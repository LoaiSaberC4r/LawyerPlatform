using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Application.Abstractions.Authentication;

public sealed record UserAuthenticationState(
    Guid UserAccountId,
    AccountStatus Status,
    int CredentialVersion);

public interface IUserAuthenticationStateReader
{
    Task<UserAuthenticationState?> ReadAsync(
        Guid userAccountId,
        CancellationToken cancellationToken = default);
}
