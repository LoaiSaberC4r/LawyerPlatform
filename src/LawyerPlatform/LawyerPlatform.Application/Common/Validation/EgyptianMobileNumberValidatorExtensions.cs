using System.Text.RegularExpressions;
using FluentValidation;

namespace LawyerPlatform.Application.Common.Validation;

internal static partial class EgyptianMobileNumberValidatorExtensions
{
    private const string EgyptianMobileNumberPattern = "^01[0125][0-9]{8}$";

    public static IRuleBuilderOptions<T, string?> EgyptianMobileNumber<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);

        return ruleBuilder.Must(phoneNumber =>
            phoneNumber is null || EgyptianMobileNumberRegex().IsMatch(phoneNumber));
    }

    [GeneratedRegex(EgyptianMobileNumberPattern, RegexOptions.CultureInvariant)]
    private static partial Regex EgyptianMobileNumberRegex();
}
