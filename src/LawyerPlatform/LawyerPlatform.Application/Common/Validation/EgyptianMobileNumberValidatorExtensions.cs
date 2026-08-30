using FluentValidation;

namespace LawyerPlatform.Application.Common.Validation;

internal static class EgyptianMobileNumberValidatorExtensions
{
    public static IRuleBuilderOptions<T, string?> EgyptianMobileNumber<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);

        return ruleBuilder.Must(phoneNumber =>
            phoneNumber is null ||
            LawyerPlatform.Domain.Common.EgyptianMobileNumber.IsValid(phoneNumber));
    }
}
