using System.Text.RegularExpressions;

namespace LawyerPlatform.Domain.Common;

public static partial class EgyptianMobileNumber
{
    private const string Pattern = "^01[0125][0-9]{8}$";

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && MobileNumberRegex().IsMatch(value.Trim());

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex MobileNumberRegex();
}
