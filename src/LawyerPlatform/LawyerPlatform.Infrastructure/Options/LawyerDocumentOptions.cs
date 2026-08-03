namespace LawyerPlatform.Infrastructure.Options;

public sealed class LawyerDocumentOptions
{
    public const string SectionName = "LawyerDocuments";

    public string[] RequiredDocumentTypes { get; init; } = [];
    public string[] AllowedExtensions { get; init; } = [];
    public string[] AllowedContentTypes { get; init; } = [];
    public long MaximumFileSizeBytes { get; init; }
}
