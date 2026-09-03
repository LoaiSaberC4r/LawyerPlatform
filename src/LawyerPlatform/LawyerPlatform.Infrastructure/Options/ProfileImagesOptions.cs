using BuildingBlock.Infrastructure.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Options;

public sealed class ProfileImagesOptions
{
    public const string SectionName = "ProfileImages";

    public string PublicPathBase { get; set; } = "/uploads";
}

internal sealed class ProfileImagesOptionsValidator(
    IWebHostEnvironment environment,
    IOptions<MediaStorageOptions> mediaStorageOptions)
    : IValidateOptions<ProfileImagesOptions>
{
    public ValidateOptionsResult Validate(string? name, ProfileImagesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!ProfileImagePathRules.IsCanonicalPublicPathBase(options.PublicPathBase))
        {
            return ValidateOptionsResult.Fail(
                "ProfileImages PublicPathBase must be a canonical root-relative path without traversal, a query string, or a fragment.");
        }

        var webRoot = Path.GetFullPath(
            environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        var expectedStorageRoot = Path.GetFullPath(Path.Combine(
            webRoot,
            options.PublicPathBase.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        var configuredStorageRoot = ProfileImagePathRules.ResolveStorageRoot(mediaStorageOptions.Value);

        if (!ProfileImagePathRules.PathsEqual(configuredStorageRoot, expectedStorageRoot))
        {
            return ValidateOptionsResult.Fail(
                "MediaStorage RootPath must match the ProfileImages public directory inside the application web root.");
        }

        return ValidateOptionsResult.Success;
    }
}

internal static class ProfileImagePathRules
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif"];

    public static bool IsCanonicalPublicPathBase(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith('/') ||
            value.StartsWith("//", StringComparison.Ordinal) ||
            value.EndsWith('/') ||
            value.Contains('\\') ||
            value.Contains(':') ||
            value.Contains('?') ||
            value.Contains('#') ||
            value.Contains('%'))
        {
            return false;
        }

        var segments = value[1..].Split('/');
        return segments.Length > 0 && segments.All(IsSafeSegment);
    }

    public static string NormalizeProfileStorageKey(string storageKey)
    {
        var normalized = storageKey.Replace('\\', '/');
        if (!string.Equals(storageKey, storageKey.Trim(), StringComparison.Ordinal) ||
            Path.IsPathRooted(storageKey) ||
            normalized.StartsWith('/') ||
            normalized.Contains(':') ||
            normalized.Contains('?') ||
            normalized.Contains('#') ||
            normalized.Contains('%'))
        {
            throw new ArgumentException("Profile image storage key is invalid.", nameof(storageKey));
        }

        var segments = normalized.Split('/');
        if (segments.Length != 4 ||
            !string.Equals(segments[0], "lawyers", StringComparison.Ordinal) ||
            !Guid.TryParseExact(segments[1], "N", out _) ||
            !string.Equals(segments[2], "profile", StringComparison.Ordinal) ||
            segments.Any(segment => !IsSafeSegment(segment)) ||
            !AllowedExtensions.Contains(Path.GetExtension(segments[3]), StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Profile image storage key is invalid.", nameof(storageKey));
        }

        return string.Join('/', segments);
    }

    public static string ResolveStorageRoot(MediaStorageOptions options)
    {
        var root = Path.IsPathRooted(options.RootPath)
            ? options.RootPath
            : Path.Combine(options.ContentRootPath ?? AppContext.BaseDirectory, options.RootPath);

        return Path.GetFullPath(root);
    }

    public static bool PathsEqual(string left, string right)
        => string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool IsSafeSegment(string segment)
        => !string.IsNullOrWhiteSpace(segment) &&
           segment is not "." and not ".." &&
           segment.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
