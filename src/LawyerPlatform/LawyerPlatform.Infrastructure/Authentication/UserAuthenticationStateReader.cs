using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LawyerPlatform.Infrastructure.Authentication;

internal sealed class UserAuthenticationStateReader(LawyerPlatformDbContext dbContext)
    : IUserAuthenticationStateReader
{
    public Task<UserAuthenticationState?> ReadAsync(
        Guid userAccountId,
        CancellationToken cancellationToken = default)
        => dbContext.UserAccounts
            .AsNoTracking()
            .Where(account => account.Id == userAccountId)
            .Select(account => new UserAuthenticationState(
                account.Id,
                account.Status,
                account.CredentialVersion))
            .SingleOrDefaultAsync(cancellationToken);
}
