using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Application.Features.Auth.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Application.Features.Auth.ChangePassword;

internal sealed class ChangePasswordCommandHandler(
    ICurrentUser currentUser,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IPasswordService passwordService,
    IDateTimeProvider clock,
    IPasswordLifecycleService passwordLifecycleService,
    IJwtProvider jwtProvider)
    : ICommandHandler<ChangePasswordCommand, AuthTokenResponse>
{
    public async Task<Result<AuthTokenResponse>> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result<AuthTokenResponse>.Fail(AuthErrors.Unauthenticated);
        }

        var account = await unitOfWork.WriteRepository<UserAccount>().GetByIdAsync(userId, cancellationToken);
        if (account is null)
        {
            return Result<AuthTokenResponse>.Fail(AccountErrors.NotFound);
        }

        var activeResult = AccountStatusGuard.EnsureActive(account);
        if (activeResult.IsFailure)
        {
            return Result<AuthTokenResponse>.Fail(activeResult.Errors);
        }

        if (!await passwordService.VerifyAsync(command.CurrentPassword, account.PasswordHash, cancellationToken))
        {
            return Result<AuthTokenResponse>.Fail(AccountErrors.CurrentPasswordInvalid);
        }

        if (await passwordService.VerifyAsync(command.NewPassword, account.PasswordHash, cancellationToken))
        {
            return Result<AuthTokenResponse>.Fail(AccountErrors.PasswordMustBeDifferent);
        }

        string passwordHash;
        try
        {
            passwordHash = await passwordService.HashAsync(command.NewPassword, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Result<AuthTokenResponse>.Fail(AuthErrors.PasswordHashingFailed);
        }

        var changeResult = account.ChangePassword(passwordHash, clock.UtcNow);
        if (changeResult.IsFailure)
        {
            return Result<AuthTokenResponse>.Fail(changeResult.Errors);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var lifecycle = passwordLifecycleService.Evaluate(account);
        var token = jwtProvider.GenerateToken(account, lifecycle);
        return Result<AuthTokenResponse>.Ok(AuthResponseFactory.Create(account, lifecycle, token));
    }
}
