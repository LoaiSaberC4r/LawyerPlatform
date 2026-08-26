using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Application.Features.Auth.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using Microsoft.Extensions.Logging;

namespace LawyerPlatform.Application.Features.Auth.ForgotPassword.ResetPassword;

internal sealed partial class ResetPasswordCommandHandler(
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IPasswordResetTokenHasher tokenHasher,
    IPasswordService passwordService,
    IDateTimeProvider clock,
    ILogger<ResetPasswordCommandHandler> logger)
    : ICommandHandler<ResetPasswordCommand, ResetPasswordResponse>
{
    public async Task<Result<ResetPasswordResponse>> Handle(
        ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var challenge = await unitOfWork
            .WriteRepository<PasswordResetChallenge>()
            .GetByIdAsync(command.RequestId, cancellationToken);
        var nowUtc = clock.UtcNow;
        if (challenge is null ||
            !challenge.CanReset(nowUtc) ||
            !tokenHasher.VerifyResetToken(command.ResetToken, challenge.ResetTokenHash!))
        {
            return Result<ResetPasswordResponse>.Fail(
                PasswordResetErrors.ResetTokenInvalidOrExpired);
        }

        var account = await unitOfWork
            .WriteRepository<UserAccount>()
            .GetByIdAsync(challenge.UserAccountId, cancellationToken);
        if (account is null)
        {
            return Result<ResetPasswordResponse>.Fail(
                PasswordResetErrors.ResetTokenInvalidOrExpired);
        }

        if (!passwordService.IsStrongPassword(command.NewPassword))
        {
            return Result<ResetPasswordResponse>.Fail(AccountErrors.NewPasswordInvalid);
        }

        if (await passwordService.VerifyAsync(
                command.NewPassword,
                account.PasswordHash,
                cancellationToken))
        {
            return Result<ResetPasswordResponse>.Fail(
                PasswordResetErrors.PasswordMustBeDifferent);
        }

        string passwordHash;
        try
        {
            passwordHash = await passwordService.HashAsync(
                command.NewPassword,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Result<ResetPasswordResponse>.Fail(AuthErrors.PasswordHashingFailed);
        }

        var changeResult = account.ChangePassword(passwordHash, nowUtc);
        if (changeResult.IsFailure)
        {
            return Result<ResetPasswordResponse>.Fail(changeResult.Errors);
        }

        var consumeResult = challenge.Consume(nowUtc);
        if (consumeResult.IsFailure)
        {
            return Result<ResetPasswordResponse>.Fail(
                PasswordResetErrors.ResetTokenInvalidOrExpired);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        PasswordResetCompleted(logger);
        return Result<ResetPasswordResponse>.Ok(new ResetPasswordResponse(true));
    }

    [LoggerMessage(
        EventId = 4403,
        Level = LogLevel.Information,
        Message = "Password reset completed.")]
    private static partial void PasswordResetCompleted(ILogger logger);
}
