using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Application.Features.Auth.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Application.Features.Auth.Login;

internal sealed class LoginQueryHandler(
    IReadRepository<UserAccount, LawyerPlatformReadPersistence> accountReader,
    IPasswordService passwordService,
    IAccountIdentifierNormalizer normalizer,
    IPasswordLifecycleService passwordLifecycleService,
    IJwtProvider jwtProvider)
    : IQueryHandler<LoginQuery, AuthTokenResponse>
{
    public async Task<Result<AuthTokenResponse>> Handle(LoginQuery query, CancellationToken cancellationToken)
    {
        var identifier = query.UserNameOrEmail.Trim();
        var account = identifier.Contains('@', StringComparison.Ordinal)
            ? await accountReader.FirstOrDefaultAsync(
                new GetUserAccountByNormalizedEmailForLoginSpec(normalizer.NormalizeEmail(identifier)),
                cancellationToken)
            : await accountReader.FirstOrDefaultAsync(
                new GetUserAccountByNormalizedUserNameForLoginSpec(normalizer.NormalizeUserName(identifier)),
                cancellationToken);

        if (account is null || !await passwordService.VerifyAsync(query.Password, account.PasswordHash, cancellationToken))
        {
            return Result<AuthTokenResponse>.Fail(AuthErrors.InvalidCredentials);
        }

        var activeResult = AccountStatusGuard.EnsureActive(account);
        if (activeResult.IsFailure)
        {
            return Result<AuthTokenResponse>.Fail(activeResult.Errors);
        }

        var lifecycle = passwordLifecycleService.Evaluate(account);
        var token = jwtProvider.GenerateToken(account, lifecycle);
        return Result<AuthTokenResponse>.Ok(AuthResponseFactory.Create(account, lifecycle, token));
    }
}
