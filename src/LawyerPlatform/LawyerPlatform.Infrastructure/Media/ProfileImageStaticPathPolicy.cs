using LawyerPlatform.Infrastructure.Options;

namespace LawyerPlatform.Infrastructure.Media;

public static class ProfileImageStaticPathPolicy
{
    public static bool IsAllowed(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !path.StartsWith('/') ||
            path.Contains('%', StringComparison.Ordinal) ||
            path.Contains('\\', StringComparison.Ordinal) ||
            path.Contains(':', StringComparison.Ordinal) ||
            path.Contains('?', StringComparison.Ordinal) ||
            path.Contains('#', StringComparison.Ordinal) ||
            path.Any(char.IsControl))
        {
            return false;
        }

        string normalized;
        try
        {
            normalized = ProfileImagePathRules.NormalizeProfileStorageKey(path[1..]);
        }
        catch (ArgumentException)
        {
            return false;
        }

        return IsSafeFileName(Path.GetFileName(normalized));
    }

    private static bool IsSafeFileName(string fileName)
        => !string.IsNullOrWhiteSpace(fileName) &&
           fileName[0] != '.' &&
           string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) &&
           fileName.All(character =>
               char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
}
