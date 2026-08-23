using LawyerPlatform.Application.Notifications.Email;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Email;

internal sealed class EmailBrandingProvider(IOptions<EmailBrandingOptions> options)
    : IEmailBrandingProvider
{
    public string FooterImageUrl => options.Value.FooterImageUrl;
}
