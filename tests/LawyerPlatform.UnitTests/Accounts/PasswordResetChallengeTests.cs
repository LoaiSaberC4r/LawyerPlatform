using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.UnitTests.Accounts;

public sealed class PasswordResetChallengeTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 26, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_SetsInitialStateAndAllowsVerificationBeforeExpiration()
    {
        var challenge = CreateChallenge();

        Assert.Equal(RequestId, challenge.Id);
        Assert.Equal(UserAccountId, challenge.UserAccountId);
        Assert.Equal("otp-hash", challenge.OtpHash);
        Assert.Equal(0, challenge.FailedAttemptCount);
        Assert.False(challenge.IsVerified);
        Assert.False(challenge.IsConsumed);
        Assert.False(challenge.IsInvalidated);
        Assert.True(challenge.CanVerify(NowUtc.AddMinutes(4), 5));
    }

    [Fact]
    public void Otp_CannotBeVerifiedAtOrAfterExpiration()
    {
        var challenge = CreateChallenge();

        Assert.True(challenge.IsExpired(NowUtc.AddMinutes(5)));
        Assert.False(challenge.CanVerify(NowUtc.AddMinutes(5), 5));
        Assert.True(challenge.Verify(NowUtc.AddMinutes(5), 5).IsFailure);
    }

    [Fact]
    public void WrongAttempts_IncrementAndMaximumInvalidatesChallenge()
    {
        var challenge = CreateChallenge();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            Assert.True(challenge.RegisterFailedAttempt(NowUtc.AddSeconds(attempt), 5).IsSuccess);
        }

        Assert.Equal(5, challenge.FailedAttemptCount);
        Assert.True(challenge.IsInvalidated);
        Assert.False(challenge.CanVerify(NowUtc.AddMinutes(1), 5));
        Assert.True(challenge.Verify(NowUtc.AddMinutes(1), 5).IsFailure);
    }

    [Fact]
    public void CorrectOtpState_VerifiesAndCanIssueSingleResetToken()
    {
        var challenge = CreateChallenge();
        var verifiedOnUtc = NowUtc.AddMinutes(1);

        Assert.True(challenge.Verify(verifiedOnUtc, 5).IsSuccess);
        Assert.True(challenge.IsVerified);
        Assert.False(challenge.CanVerify(verifiedOnUtc, 5));
        Assert.True(challenge.IssueResetToken(
            "reset-hash",
            verifiedOnUtc.AddMinutes(10),
            verifiedOnUtc).IsSuccess);
        Assert.True(challenge.CanReset(verifiedOnUtc.AddMinutes(9)));
        Assert.False(challenge.CanReset(verifiedOnUtc.AddMinutes(10)));
        Assert.True(challenge.IssueResetToken(
            "replacement-hash",
            verifiedOnUtc.AddMinutes(10),
            verifiedOnUtc).IsFailure);
    }

    [Fact]
    public void UnverifiedChallenge_CannotIssueResetToken()
    {
        var challenge = CreateChallenge();

        var result = challenge.IssueResetToken(
            "reset-hash",
            NowUtc.AddMinutes(10),
            NowUtc);

        Assert.True(result.IsFailure);
        Assert.Null(challenge.ResetTokenHash);
    }

    [Fact]
    public void ConsumedChallenge_CannotBeReused()
    {
        var challenge = CreateVerifiedChallenge();

        Assert.True(challenge.Consume(NowUtc.AddMinutes(2)).IsSuccess);
        Assert.True(challenge.IsConsumed);
        Assert.False(challenge.CanReset(NowUtc.AddMinutes(3)));
        Assert.True(challenge.Consume(NowUtc.AddMinutes(3)).IsFailure);
        Assert.True(challenge.Verify(NowUtc.AddMinutes(3), 5).IsFailure);
    }

    [Fact]
    public void InvalidatedChallenge_CannotBeVerifiedOrConsumed()
    {
        var challenge = CreateChallenge();

        Assert.True(challenge.Invalidate(NowUtc.AddMinutes(1)).IsSuccess);

        Assert.True(challenge.IsInvalidated);
        Assert.False(challenge.CanVerify(NowUtc.AddMinutes(2), 5));
        Assert.True(challenge.Verify(NowUtc.AddMinutes(2), 5).IsFailure);
        Assert.True(challenge.Consume(NowUtc.AddMinutes(2)).IsFailure);
    }

    private static PasswordResetChallenge CreateChallenge()
        => PasswordResetChallenge.Create(
            RequestId,
            UserAccountId,
            "otp-hash",
            NowUtc,
            NowUtc.AddMinutes(5)).Value;

    private static PasswordResetChallenge CreateVerifiedChallenge()
    {
        var challenge = CreateChallenge();
        Assert.True(challenge.Verify(NowUtc.AddMinutes(1), 5).IsSuccess);
        Assert.True(challenge.IssueResetToken(
            "reset-hash",
            NowUtc.AddMinutes(11),
            NowUtc.AddMinutes(1)).IsSuccess);
        return challenge;
    }

    private static readonly Guid RequestId = Guid.Parse("37d7f255-5a77-437f-9941-c67800b93209");
    private static readonly Guid UserAccountId = Guid.Parse("d44b71a4-4fb5-45c0-bcd0-c79e67c39931");
}
