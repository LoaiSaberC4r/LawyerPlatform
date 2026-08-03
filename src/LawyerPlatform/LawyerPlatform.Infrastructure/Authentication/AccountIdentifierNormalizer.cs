using LawyerPlatform.Application.Abstractions.Authentication;

namespace LawyerPlatform.Infrastructure.Authentication;

internal sealed class AccountIdentifierNormalizer : IAccountIdentifierNormalizer
{
    public string NormalizeUserName(string userName)
        => (userName ?? string.Empty).Trim().ToUpperInvariant();

    public string NormalizeEmail(string email)
        => (email ?? string.Empty).Trim().ToUpperInvariant();
}
