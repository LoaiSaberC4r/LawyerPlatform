using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Options;

public sealed class FrontendUrlsOptions
{
    public const string SectionName = "FrontendUrls";

    public string ConsultationTrackingUrl { get; init; } = string.Empty;
}

internal sealed class FrontendUrlsOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<FrontendUrlsOptions>
{
    public ValidateOptionsResult Validate(string? name, FrontendUrlsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ConsultationTrackingUrl))
        {
            return ValidateOptionsResult.Fail(
                "FrontendUrls ConsultationTrackingUrl is required.");
        }

        if (!Uri.TryCreate(options.ConsultationTrackingUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return ValidateOptionsResult.Fail(
                "FrontendUrls ConsultationTrackingUrl must be an absolute HTTP or HTTPS URL without credentials, a query string, or a fragment.");
        }

        if (environment.IsProduction() && uri.Scheme != Uri.UriSchemeHttps)
        {
            return ValidateOptionsResult.Fail(
                "FrontendUrls ConsultationTrackingUrl must use HTTPS in Production.");
        }

        return ValidateOptionsResult.Success;
    }
}
