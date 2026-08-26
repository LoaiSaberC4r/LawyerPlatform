using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.Accounts;

public sealed class PasswordResetChallenge : AggregateRoot<Guid>
{
    private PasswordResetChallenge()
    {
    }

    private PasswordResetChallenge(
        Guid id,
        Guid userAccountId,
        string otpHash,
        DateTime createdOnUtc,
        DateTime expiresOnUtc)
        : base(id)
    {
        UserAccountId = userAccountId;
        OtpHash = otpHash;
        CreatedOnUtc = createdOnUtc;
        ExpiresOnUtc = expiresOnUtc;
    }

    public Guid UserAccountId { get; private set; }
    public string OtpHash { get; private set; } = string.Empty;
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime ExpiresOnUtc { get; private set; }
    public int FailedAttemptCount { get; private set; }
    public DateTime? VerifiedOnUtc { get; private set; }
    public DateTime? InvalidatedOnUtc { get; private set; }
    public DateTime? ConsumedOnUtc { get; private set; }
    public string? ResetTokenHash { get; private set; }
    public DateTime? ResetTokenExpiresOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsVerified => VerifiedOnUtc.HasValue;
    public bool IsConsumed => ConsumedOnUtc.HasValue;
    public bool IsInvalidated => InvalidatedOnUtc.HasValue;

    public static Result<PasswordResetChallenge> Create(
        Guid id,
        Guid userAccountId,
        string otpHash,
        DateTime createdOnUtc,
        DateTime expiresOnUtc)
    {
        if (id == Guid.Empty ||
            userAccountId == Guid.Empty ||
            string.IsNullOrWhiteSpace(otpHash) ||
            expiresOnUtc <= createdOnUtc)
        {
            return Result<PasswordResetChallenge>.Fail(PasswordResetErrors.ConfigurationInvalid);
        }

        return Result<PasswordResetChallenge>.Ok(new PasswordResetChallenge(
            id,
            userAccountId,
            otpHash,
            createdOnUtc,
            expiresOnUtc));
    }

    public bool IsExpired(DateTime nowUtc) => ExpiresOnUtc <= nowUtc;

    public bool CanVerify(DateTime nowUtc, int maximumVerificationAttempts)
        => maximumVerificationAttempts > 0 &&
           !IsInvalidated &&
           !IsConsumed &&
           !IsVerified &&
           !IsExpired(nowUtc) &&
           FailedAttemptCount < maximumVerificationAttempts;

    public Result RegisterFailedAttempt(DateTime nowUtc, int maximumVerificationAttempts)
    {
        if (!CanVerify(nowUtc, maximumVerificationAttempts))
        {
            return Result.Fail(PasswordResetErrors.OtpInvalidOrExpired);
        }

        FailedAttemptCount++;
        if (FailedAttemptCount >= maximumVerificationAttempts)
        {
            InvalidatedOnUtc = nowUtc;
        }

        return Result.Ok();
    }

    public Result Verify(DateTime nowUtc, int maximumVerificationAttempts)
    {
        if (!CanVerify(nowUtc, maximumVerificationAttempts))
        {
            return Result.Fail(PasswordResetErrors.OtpInvalidOrExpired);
        }

        VerifiedOnUtc = nowUtc;
        return Result.Ok();
    }

    public Result IssueResetToken(
        string resetTokenHash,
        DateTime resetTokenExpiresOnUtc,
        DateTime nowUtc)
    {
        if (!IsVerified ||
            IsInvalidated ||
            IsConsumed ||
            ResetTokenHash is not null ||
            string.IsNullOrWhiteSpace(resetTokenHash) ||
            resetTokenExpiresOnUtc <= nowUtc)
        {
            return Result.Fail(PasswordResetErrors.ResetTokenInvalidOrExpired);
        }

        ResetTokenHash = resetTokenHash;
        ResetTokenExpiresOnUtc = resetTokenExpiresOnUtc;
        return Result.Ok();
    }

    public bool CanReset(DateTime nowUtc)
        => IsVerified &&
           !IsInvalidated &&
           !IsConsumed &&
           ResetTokenHash is not null &&
           ResetTokenExpiresOnUtc is not null &&
           ResetTokenExpiresOnUtc > nowUtc;

    public Result Invalidate(DateTime nowUtc)
    {
        if (IsConsumed)
        {
            return Result.Fail(PasswordResetErrors.ChallengeConsumed);
        }

        InvalidatedOnUtc ??= nowUtc;
        return Result.Ok();
    }

    public Result Consume(DateTime nowUtc)
    {
        if (!CanReset(nowUtc))
        {
            return Result.Fail(IsConsumed
                ? PasswordResetErrors.ChallengeConsumed
                : PasswordResetErrors.ResetTokenInvalidOrExpired);
        }

        ConsumedOnUtc = nowUtc;
        return Result.Ok();
    }
}
