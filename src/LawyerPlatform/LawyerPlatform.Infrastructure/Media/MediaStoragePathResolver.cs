using BuildingBlock.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Media;

internal interface IMediaStoragePathResolver
{
    string RootPath { get; }

    bool TryResolveStorageKey(string storageKey, out string physicalPath);
}

internal sealed class MediaStoragePathResolver : IMediaStoragePathResolver
{
    private readonly StringComparison _pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public MediaStoragePathResolver(IOptions<MediaStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        RootPath = ResolveRoot(options.Value.RootPath, options.Value.ContentRootPath);
    }

    public string RootPath { get; }

    public bool TryResolveStorageKey(string storageKey, out string physicalPath)
    {
        physicalPath = string.Empty;
        if (string.IsNullOrWhiteSpace(storageKey) ||
            !string.Equals(storageKey, storageKey.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        var normalized = storageKey.Replace('\\', '/');
        if (Path.IsPathRooted(storageKey) ||
            normalized.StartsWith('/') ||
            normalized.Contains(':', StringComparison.Ordinal) ||
            normalized.Contains('?', StringComparison.Ordinal) ||
            normalized.Contains('#', StringComparison.Ordinal) ||
            normalized.Contains('%', StringComparison.Ordinal))
        {
            return false;
        }

        var segments = normalized.Split('/');
        if (segments.Any(segment =>
                string.IsNullOrWhiteSpace(segment) ||
                segment is "." or ".." ||
                segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            return false;
        }

        string candidate;
        try
        {
            candidate = Path.GetFullPath(Path.Combine(
                RootPath,
                normalized.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        var rootPrefix = Path.TrimEndingDirectorySeparator(RootPath) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, _pathComparison))
        {
            return false;
        }

        physicalPath = candidate;
        return true;
    }

    internal static string ResolveRoot(string configuredRootPath, string? contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredRootPath) ||
            !string.Equals(configuredRootPath, configuredRootPath.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Media storage root path is required and cannot contain surrounding whitespace.", nameof(configuredRootPath));
        }

        var basePath = string.IsNullOrWhiteSpace(contentRootPath)
            ? AppContext.BaseDirectory
            : contentRootPath;
        var isAbsolute = Path.IsPathFullyQualified(configuredRootPath);
        if ((Path.IsPathRooted(configuredRootPath) && !isAbsolute) ||
            (!isAbsolute && configuredRootPath.Contains(':', StringComparison.Ordinal)))
        {
            throw new ArgumentException("Media storage root cannot be drive-relative.", nameof(configuredRootPath));
        }

        if (!isAbsolute)
        {
            var segments = configuredRootPath.Replace('\\', '/').Split('/');
            if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
            {
                throw new ArgumentException("Relative media storage root contains an unsafe segment.", nameof(configuredRootPath));
            }
        }

        var candidate = isAbsolute
            ? configuredRootPath
            : Path.Combine(basePath, configuredRootPath);
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        var volumeRoot = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(fullPath) ?? string.Empty);

        if (string.IsNullOrWhiteSpace(fullPath) ||
            string.Equals(
                fullPath,
                volumeRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new ArgumentException("Media storage root cannot be a filesystem root.", nameof(configuredRootPath));
        }

        return fullPath;
    }
}
