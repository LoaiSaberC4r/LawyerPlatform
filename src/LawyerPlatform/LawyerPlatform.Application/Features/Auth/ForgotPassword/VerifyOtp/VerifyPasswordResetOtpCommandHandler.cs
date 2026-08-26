using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using Microsoft.Extensions.Logging;

namespace LawyerPlatform.Application.Features.Auth.ForgotPassword.VerifyOtp;

internal sealed partial class VerifyPasswordResetOtpCommandHandler(
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IPasswordResetTokenHasher tokenHasher,
    IPasswordResetSecretGenerator secretGenerator,
    IPasswordResetPolicy policy,
    IDateTimeProvider clock,
    ILogger<VerifyPasswordResetOtpCommandHandler> logger)
    : ICommandHandler<VerifyPasswordResetOtpCommand, VerifyPasswordResetOtpResponse>
{
    public async Task<Result<VerifyPasswordResetOtpResponse>> Handle(
        VerifyPasswordResetOtpCommand command,
        CancellationToken cancellationToken)
    {
        var challenge = await unitOfWork
            .WriteRepository<PasswordResetChallenge>()
            .GetByIdAsync(command.RequestId, cancellationToken);
        var nowUtc = clock.UtcNow;

        if (challenge is null ||
            !challenge.CanVerify(nowUtc, policy.MaximumVerificationAttempts))
        {
            PasswordResetOtpVerificationFailed(logger);
            return Result<VerifyPasswordResetOtpResponse>.Fail(
                PasswordResetErrors.OtpInvalidOrExpired);
        }

        if (!tokenHasher.VerifyOtp(command.Otp, challenge.OtpHash))
        {
            var failedAttempt = challenge.RegisterFailedAttempt(
                nowUtc,
                policy.MaximumVerificationAttempts);
            if (failedAttempt.IsSuccess)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            PasswordResetOtpVerificationFailed(logger);
            return Result<VerifyPasswordResetOtpResponse>.Fail(
                PasswordResetErrors.OtpInvalidOrExpired);
        }

        var verifyResult = challenge.Verify(nowUtc, policy.MaximumVerificationAttempts);
        if (verifyResult.IsFailure)
        {
            PasswordResetOtpVerificationFailed(logger);
            return Result<VerifyPasswordResetOtpResponse>.Fail(
                PasswordResetErrors.OtpInvalidOrExpired);
        }

        var resetToken = secretGenerator.GenerateResetToken();
        var issueResult = challenge.IssueResetToken(
            tokenHasher.HashResetToken(resetToken),
            nowUtc.AddMinutes(policy.ResetTokenExpirationMinutes),
            nowUtc);
        if (issueResult.IsFailure)
        {
            return Result<VerifyPasswordResetOtpResponse>.Fail(
                PasswordResetErrors.OtpInvalidOrExpired);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<VerifyPasswordResetOtpResponse>.Ok(new VerifyPasswordResetOtpResponse(
            resetToken,
            checked(policy.ResetTokenExpirationMinutes * 60)));
    }

    [LoggerMessage(
        EventId = 4402,
        Level = LogLevel.Information,
        Message = "Password reset OTP verification failed.")]
    private static partial void PasswordResetOtpVerificationFailed(ILogger logger);
}
