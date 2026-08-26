using System.Globalization;
using System.Security.Cryptography;
using LawyerPlatform.Application.Abstractions.Authentication;

namespace LawyerPlatform.Infrastructure.Authentication;

internal sealed class PasswordResetSecretGenerator : IPasswordResetSecretGenerator
{
    public string GenerateOtp()
        => RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString("D6", CultureInfo.InvariantCulture);

    public string GenerateResetToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
