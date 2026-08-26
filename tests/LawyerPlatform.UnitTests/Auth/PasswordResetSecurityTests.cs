using BuildingBlock.Application.Time;
using LawyerPlatform.Application.Notifications.Email;
using LawyerPlatform.Infrastructure.Authentication;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.UnitTests.Auth;

public sealed class PasswordResetSecurityTests
{
    private const string Secret = "unit-test-password-reset-hmac-secret-0001";

    [Fact]
    public void Hasher_UsesDomainSeparatedHmacAndVerifiesValues()
    {
        var hasher = CreateHasher();
        const string value = "483921";

        var otpHash = hasher.HashOtp(value);
        var resetHash = hasher.HashResetToken(value);

        Assert.NotEqual(value, otpHash);
        Assert.NotEqual(otpHash, resetHash);
        Assert.True(hasher.VerifyOtp(value, otpHash));
        Assert.False(hasher.VerifyOtp("483922", otpHash));
        Assert.True(hasher.VerifyResetToken(value, resetHash));
        Assert.False(hasher.VerifyResetToken(value, otpHash));
        Assert.False(hasher.VerifyOtp(value, "not-base64"));
    }

    [Fact]
    public void Generator_CreatesSixDigitOtpAndBase64UrlResetToken()
    {
        var generator = new PasswordResetSecretGenerator();

        var otp = generator.GenerateOtp();
        var firstToken = generator.GenerateResetToken();
        var secondToken = generator.GenerateResetToken();

        Assert.Matches("^[0-9]{6}$", otp);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", firstToken);
        Assert.NotEqual(firstToken, secondToken);
    }

    [Fact]
    public void OtpEmail_IsBilingualAvokatooBrandedAndContainsNoResetToken()
    {
        var factory = new PasswordResetOtpEmailFactory(
            new FixedClock(new DateTime(2026, 8, 26, 8, 0, 0, DateTimeKind.Utc)),
            new FixedBrandingProvider("https://cdn.example.test/avokatoo-footer.png"));

        var message = factory.Create("client@example.test", "000123", 5);

        Assert.Equal(
            "Avokatoo | رمز إعادة تعيين كلمة المرور | Password Reset Code",
            message.Subject);
        Assert.Equal("client@example.test", Assert.Single(message.To));
        Assert.Contains("000123", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("lang=\"ar\"", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("lang=\"en\"", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Avokatoo", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Lawyer Platform", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("resetToken", message.HtmlBody, StringComparison.OrdinalIgnoreCase);
    }

    private static PasswordResetTokenHasher CreateHasher()
        => new(Options.Create(new PasswordResetOptions { HmacSecret = Secret }));

    private sealed class FixedClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FixedBrandingProvider(string footerImageUrl) : IEmailBrandingProvider
    {
        public string FooterImageUrl { get; } = footerImageUrl;
    }
}
