using System.Security.Cryptography;
using System.Text;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Authentication;

internal sealed class PasswordResetTokenHasher : IPasswordResetTokenHasher
{
    private const string OtpDomain = "otp:";
    private const string ResetTokenDomain = "reset:";
    private readonly byte[] _secret;

    public PasswordResetTokenHasher(IOptions<PasswordResetOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _secret = Encoding.UTF8.GetBytes(options.Value.HmacSecret);
    }

    public string HashOtp(string otp) => Hash(OtpDomain, otp);

    public bool VerifyOtp(string otp, string otpHash) => Verify(OtpDomain, otp, otpHash);

    public string HashResetToken(string resetToken) => Hash(ResetTokenDomain, resetToken);

    public bool VerifyResetToken(string resetToken, string resetTokenHash)
        => Verify(ResetTokenDomain, resetToken, resetTokenHash);

    private string Hash(string domain, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var input = Encoding.UTF8.GetBytes(domain + value);
        return Convert.ToBase64String(HMACSHA256.HashData(_secret, input));
    }

    private bool Verify(string domain, string value, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        byte[] expectedBytes;
        try
        {
            expectedBytes = Convert.FromBase64String(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var input = Encoding.UTF8.GetBytes(domain + value);
        var actualBytes = HMACSHA256.HashData(_secret, input);
        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}
