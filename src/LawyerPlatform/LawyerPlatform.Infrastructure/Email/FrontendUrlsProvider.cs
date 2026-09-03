using LawyerPlatform.Application.Notifications.Email;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Email;

internal sealed class FrontendUrlsProvider(IOptions<FrontendUrlsOptions> options)
    : IFrontendUrlsProvider
{
    public string ConsultationTrackingUrl => options.Value.ConsultationTrackingUrl;
}
