using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Options;

public sealed class EmailBrandingOptions
{
    public const string SectionName = "EmailBranding";

    public string FooterImageUrl { get; set; } = string.Empty;
}

internal sealed class EmailBrandingOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<EmailBrandingOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailBrandingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.FooterImageUrl))
        {
            return ValidateOptionsResult.Fail(
                "EmailBranding FooterImageUrl is required.");
        }

        if (!Uri.TryCreate(options.FooterImageUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return ValidateOptionsResult.Fail(
                "EmailBranding FooterImageUrl must be an absolute HTTP or HTTPS URL without credentials, a query string, or a fragment.");
        }

        if (environment.IsProduction() && uri.Scheme != Uri.UriSchemeHttps)
        {
            return ValidateOptionsResult.Fail(
                "EmailBranding FooterImageUrl must use HTTPS in Production.");
        }

        return ValidateOptionsResult.Success;
    }
}
