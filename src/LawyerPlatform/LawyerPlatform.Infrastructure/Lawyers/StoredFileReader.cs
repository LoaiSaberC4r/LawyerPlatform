using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Exceptions;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Infrastructure.Media;

namespace LawyerPlatform.Infrastructure.Lawyers;

internal sealed class StoredFileReader(IMediaStoragePathResolver pathResolver)
    : IStoredFileReader, IStoredFileAvailability
{
    public Task<bool> ExistsAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            pathResolver.TryResolveStorageKey(storageKey, out var path) && File.Exists(path));
    }

    public Task<StoredFileContent?> OpenReadAsync(
        string storageKey,
        string downloadFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!pathResolver.TryResolveStorageKey(storageKey, out var path) || !File.Exists(path))
        {
            return Task.FromResult<StoredFileContent?>(null);
        }

        try
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            var safeName = Path.GetFileName(downloadFileName);
            var detectedContentType = contentType == "application/octet-stream"
                ? GetContentType(Path.GetExtension(path))
                : contentType;
            return Task.FromResult<StoredFileContent?>(new StoredFileContent(
                stream,
                detectedContentType,
                string.IsNullOrWhiteSpace(safeName) ? "download" : safeName,
                stream.Length));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<StoredFileContent?>(null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new MediaServiceException(
                Error.Infra(
                    ExternalServiceErrorCodes.Media.StorageUnavailable,
                    "Stored media content is unavailable.",
                    source: "Media"),
                exception);
        }
    }

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };
}
