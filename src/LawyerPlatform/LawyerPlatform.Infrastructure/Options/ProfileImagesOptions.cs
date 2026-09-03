using LawyerPlatform.Infrastructure.Media;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Options;

public sealed class ProfileImagesOptions
{
    public const string SectionName = "ProfileImages";

    public string PublicPathBase { get; set; } = "/uploads";
}

internal sealed class ProfileImagesOptionsValidator(IMediaStoragePathResolver mediaStoragePathResolver)
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

        var physicalRoot = mediaStoragePathResolver.RootPath;
        if (!Path.IsPathFullyQualified(physicalRoot) ||
            !string.Equals(
                Path.GetFullPath(physicalRoot),
                physicalRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "MediaStorage RootPath must resolve to a canonical absolute physical path.");
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
            !Guid.TryParseExact(segments[1], "N", out var lawyerId) ||
            lawyerId == Guid.Empty ||
            !string.Equals(segments[2], "profile", StringComparison.Ordinal) ||
            segments.Any(segment => !IsSafeSegment(segment)) ||
            !AllowedExtensions.Contains(Path.GetExtension(segments[3]), StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Profile image storage key is invalid.", nameof(storageKey));
        }

        return string.Join('/', segments);
    }

    private static bool IsSafeSegment(string segment)
        => !string.IsNullOrWhiteSpace(segment) &&
           segment is not "." and not ".." &&
           segment.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
