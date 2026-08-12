namespace LawyerPlatform.Infrastructure.Options;

public sealed class ConsultationSchedulingOptions
{
    public const string SectionName = "ConsultationScheduling";

    public string TimeZoneId { get; init; } = string.Empty;
}
