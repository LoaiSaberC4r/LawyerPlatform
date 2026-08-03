using BuildingBlock.Application.Time;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Infrastructure.Authentication;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.UnitTests.Accounts;

public sealed class PasswordLifecycleTests
{
    private static readonly DateTime ChangedOnUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void Evaluate_UsesInclusiveExpiryBoundary(int secondsFromExpiry, bool expectedExpired)
    {
        var account = UserAccount.CreateClient(
            "client.one", "CLIENT.ONE", "client@example.test", "CLIENT@EXAMPLE.TEST",
            "01000000001", "hash", ChangedOnUtc).Value;
        var clock = new FixedClock(ChangedOnUtc.AddDays(90).AddSeconds(secondsFromExpiry));
        var service = new PasswordLifecycleService(
            Options.Create(new PasswordLifecycleOptions { ExpiryDays = 90 }), clock);

        var result = service.Evaluate(account);

        Assert.Equal(expectedExpired, result.PasswordExpired);
        Assert.Equal(expectedExpired, result.PasswordChangeRequired);
        Assert.Equal(expectedExpired ? PasswordChangeReason.Expired : PasswordChangeReason.None, result.PasswordChangeReason);
    }

    [Fact]
    public void Evaluate_FirstLoginAlwaysRequiresPasswordChange()
    {
        var account = UserAccount.CreateSuperAdmin(
            Guid.NewGuid(), "superadmin", "SUPERADMIN", "admin@example.test", "ADMIN@EXAMPLE.TEST",
            "01000000000", "hash", ChangedOnUtc).Value;
        var service = new PasswordLifecycleService(
            Options.Create(new PasswordLifecycleOptions { ExpiryDays = 90 }),
            new FixedClock(ChangedOnUtc.AddDays(1)));

        var result = service.Evaluate(account);

        Assert.False(result.PasswordExpired);
        Assert.True(result.PasswordChangeRequired);
        Assert.Equal(PasswordChangeReason.FirstLogin, result.PasswordChangeReason);
    }

    private sealed class FixedClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
