namespace LawyerPlatform.Infrastructure.Options;

public sealed class ContactUsOptions
{
    public const string SectionName = "ContactUs";

    public string SupportEmail { get; set; } = string.Empty;
}
