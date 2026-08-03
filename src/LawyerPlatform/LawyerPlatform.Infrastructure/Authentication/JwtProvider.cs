using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BuildingBlock.Application.Time;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LawyerPlatform.Infrastructure.Authentication;

internal sealed class JwtProvider(
    IOptions<JwtOptions> options,
    IDateTimeProvider clock)
    : IJwtProvider
{
    public JwtToken GenerateToken(UserAccount account, PasswordLifecycleState passwordLifecycle)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(passwordLifecycle);

        var jwtOptions = options.Value;
        var nowUtc = clock.UtcNow;
        var regularExpiry = nowUtc.AddMinutes(jwtOptions.AccessTokenExpirationMinutes);
        var effectiveExpiry = passwordLifecycle.PasswordChangeRequired
            ? regularExpiry
            : new DateTime(Math.Min(regularExpiry.Ticks, passwordLifecycle.PasswordExpiresOnUtc.Ticks), DateTimeKind.Utc);

        var claims = new[]
        {
            new Claim(LawyerPlatformClaimTypes.Subject, account.Id.ToString()),
            new Claim(LawyerPlatformClaimTypes.Email, account.Email),
            new Claim(LawyerPlatformClaimTypes.PreferredUserName, account.UserName),
            new Claim(LawyerPlatformClaimTypes.Role, account.Role.ToString()),
            new Claim(LawyerPlatformClaimTypes.PasswordChangeRequired, passwordLifecycle.PasswordChangeRequired.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()),
            new Claim(LawyerPlatformClaimTypes.PasswordChangeReason, passwordLifecycle.PasswordChangeReason.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: nowUtc,
            expires: effectiveExpiry,
            signingCredentials: credentials);

        return new JwtToken(new JwtSecurityTokenHandler().WriteToken(token), effectiveExpiry);
    }
}
