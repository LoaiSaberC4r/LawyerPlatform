using LawyerPlatform.Application.Abstractions.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.Common;

internal sealed record StoredDocumentReference(string DocumentType, string StorageKey);

internal static class RequiredDocumentAvailability
{
    public static async Task<IReadOnlyList<string>> GetAvailableTypesAsync(
        IReadOnlyCollection<StoredDocumentReference> documents,
        ILawyerDocumentPolicy documentPolicy,
        IStoredFileAvailability storedFileAvailability,
        CancellationToken cancellationToken)
    {
        var availableDocumentTypes = new List<string>();
        foreach (var requiredDocumentType in documentPolicy.RequiredDocumentTypes)
        {
            var document = documents.FirstOrDefault(item =>
                string.Equals(item.DocumentType, requiredDocumentType, StringComparison.OrdinalIgnoreCase));
            if (document is not null &&
                await storedFileAvailability.ExistsAsync(document.StorageKey, cancellationToken))
            {
                availableDocumentTypes.Add(document.DocumentType);
            }
        }

        return availableDocumentTypes;
    }
}
