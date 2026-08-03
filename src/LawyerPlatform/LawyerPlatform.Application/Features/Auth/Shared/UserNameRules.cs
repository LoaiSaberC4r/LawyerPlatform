using System.Text.RegularExpressions;

namespace LawyerPlatform.Application.Features.Auth.Common;

public static partial class UserNameRules
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 50;

    public static bool IsValid(string userName)
        => !string.IsNullOrWhiteSpace(userName) && UserNamePattern().IsMatch(userName);

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9._-]{1,48}[A-Za-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex UserNamePattern();
}
