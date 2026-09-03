namespace LawyerPlatform.Api.Middleware;

internal static class ProfileImageStaticPathPolicy
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif"];

    public static bool IsAllowed(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrEmpty(value) ||
            value.Contains('%') ||
            value.Contains('\\'))
        {
            return false;
        }

        var segments = value.Split('/');
        return segments.Length == 5 &&
               segments[0].Length == 0 &&
               string.Equals(segments[1], "lawyers", StringComparison.Ordinal) &&
               Guid.TryParseExact(segments[2], "N", out _) &&
               string.Equals(segments[3], "profile", StringComparison.Ordinal) &&
               segments[4] is not "." and not ".." &&
               string.Equals(Path.GetFileName(segments[4]), segments[4], StringComparison.Ordinal) &&
               AllowedExtensions.Contains(Path.GetExtension(segments[4]), StringComparer.OrdinalIgnoreCase);
    }
}
