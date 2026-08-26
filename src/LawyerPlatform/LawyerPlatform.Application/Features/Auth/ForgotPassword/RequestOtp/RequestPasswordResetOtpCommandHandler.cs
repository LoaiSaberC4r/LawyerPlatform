using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Email;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Application.Notifications.Email;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using Microsoft.Extensions.Logging;

namespace LawyerPlatform.Application.Features.Auth.ForgotPassword.RequestOtp;

internal sealed partial class RequestPasswordResetOtpCommandHandler(
    IReadRepository<UserAccount, LawyerPlatformReadPersistence> accountReader,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IAccountIdentifierNormalizer normalizer,
    IPasswordResetSecretGenerator secretGenerator,
    IPasswordResetTokenHasher tokenHasher,
    IPasswordResetPolicy policy,
    IDateTimeProvider clock,
    IPasswordResetOtpEmailFactory emailFactory,
    IEmailSender emailSender,
    ILogger<RequestPasswordResetOtpCommandHandler> logger)
    : ICommandHandler<RequestPasswordResetOtpCommand, RequestPasswordResetOtpResponse>
{
    public async Task<Result<RequestPasswordResetOtpResponse>> Handle(
        RequestPasswordResetOtpCommand command,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid();
        var response = CreateResponse(requestId);
        var normalizedEmail = normalizer.NormalizeEmail(command.Email);
        var account = await accountReader.FirstOrDefaultAsync(
            new PasswordResetAccountByNormalizedEmailSpec(normalizedEmail),
            cancellationToken);

        if (account is null)
        {
            PasswordResetOtpRequested(logger);
            return Result<RequestPasswordResetOtpResponse>.Ok(response);
        }

        var nowUtc = clock.UtcNow;
        var challengeRepository = unitOfWork.WriteRepository<PasswordResetChallenge>();
        var latestChallenge = await challengeRepository.FirstOrDefaultAsync(
            new LatestPasswordResetChallengeForAccountSpec(account.Id),
            cancellationToken);
        if (latestChallenge is not null &&
            latestChallenge.CreatedOnUtc.AddSeconds(policy.ResendCooldownSeconds) > nowUtc)
        {
            PasswordResetOtpRequested(logger);
            return Result<RequestPasswordResetOtpResponse>.Ok(response);
        }

        if (latestChallenge is not null &&
            !latestChallenge.IsInvalidated &&
            !latestChallenge.IsConsumed)
        {
            var invalidateResult = latestChallenge.Invalidate(nowUtc);
            if (invalidateResult.IsFailure)
            {
                return Result<RequestPasswordResetOtpResponse>.Fail(invalidateResult.Errors);
            }
        }

        var otp = secretGenerator.GenerateOtp();
        var challengeResult = PasswordResetChallenge.Create(
            requestId,
            account.Id,
            tokenHasher.HashOtp(otp),
            nowUtc,
            nowUtc.AddMinutes(policy.OtpExpirationMinutes));
        if (challengeResult.IsFailure)
        {
            return Result<RequestPasswordResetOtpResponse>.Fail(challengeResult.Errors);
        }

        await challengeRepository.AddAsync(challengeResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var email = emailFactory.Create(account.Email, otp, policy.OtpExpirationMinutes);
            await emailSender.SendAsync(email, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            PasswordResetOtpDeliveryFailed(logger);
        }

        PasswordResetOtpRequested(logger);
        return Result<RequestPasswordResetOtpResponse>.Ok(response);
    }

    private RequestPasswordResetOtpResponse CreateResponse(Guid requestId)
        => new(
            requestId,
            checked(policy.OtpExpirationMinutes * 60),
            policy.ResendCooldownSeconds);

    [LoggerMessage(
        EventId = 4400,
        Level = LogLevel.Information,
        Message = "Password reset OTP requested.")]
    private static partial void PasswordResetOtpRequested(ILogger logger);

    [LoggerMessage(
        EventId = 4401,
        Level = LogLevel.Warning,
        Message = "Password reset OTP delivery failed.")]
    private static partial void PasswordResetOtpDeliveryFailed(ILogger logger);
}
