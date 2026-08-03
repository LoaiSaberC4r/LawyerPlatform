using BuildingBlock.Infrastructure.Options;
using LawyerPlatform.Application.Abstractions.Lawyers;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Lawyers;

internal sealed class StoredFileReader : IStoredFileReader
{
    private readonly string _root;

    public StoredFileReader(IOptions<MediaStorageOptions> options)
    {
        var value = options.Value;
        _root = Path.GetFullPath(Path.IsPathRooted(value.RootPath)
            ? value.RootPath
            : Path.Combine(value.ContentRootPath ?? AppContext.BaseDirectory, value.RootPath));
    }

    public Task<StoredFileContent?> OpenReadAsync(
        string storageKey,
        string downloadFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return Task.FromResult<StoredFileContent?>(null);
        }

        var normalized = storageKey.Trim().Replace('\\', '/').Trim('/');
        if (Path.IsPathRooted(normalized) || normalized.Contains(':', StringComparison.Ordinal) ||
            normalized.Split('/').Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            return Task.FromResult<StoredFileContent?>(null);
        }

        var path = Path.GetFullPath(Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return Task.FromResult<StoredFileContent?>(null);
        }

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

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };
}
