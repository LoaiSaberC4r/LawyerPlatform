using LawyerPlatform.Application.Abstractions.ContactInquiries;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.ContactInquiries;

internal sealed class ContactUsRecipientProvider(IOptions<ContactUsOptions> options)
    : IContactUsRecipientProvider
{
    public string SupportEmail { get; } = options.Value.SupportEmail.Trim();
}
