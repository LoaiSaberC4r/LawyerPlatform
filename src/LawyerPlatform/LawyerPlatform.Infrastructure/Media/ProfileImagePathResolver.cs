using LawyerPlatform.Application.Abstractions.Media;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Media;

internal sealed class ProfileImagePathResolver(IOptions<ProfileImagesOptions> options)
    : IProfileImagePathResolver
{
    private readonly string _publicPathBase = options.Value.PublicPathBase;

    public string? Resolve(string? profileImageStorageKey)
    {
        if (string.IsNullOrWhiteSpace(profileImageStorageKey))
        {
            return null;
        }

        var normalizedKey = ProfileImagePathRules.NormalizeProfileStorageKey(profileImageStorageKey);
        return $"{_publicPathBase}/{normalizedKey}";
    }
}
