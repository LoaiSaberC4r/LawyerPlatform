using LawyerPlatform.Infrastructure.Media;

namespace LawyerPlatform.UnitTests.Media;

public sealed class ProfileImageStaticPathPolicyTests
{
    private const string LawyerId = "945e8e5016984202b634590e5477d495";

    [Theory]
    [InlineData($"/lawyers/{LawyerId}/profile/image.jpg")]
    [InlineData($"/lawyers/{LawyerId}/profile/image.JPEG")]
    [InlineData($"/lawyers/{LawyerId}/profile/image.png")]
    [InlineData($"/lawyers/{LawyerId}/profile/image.gif")]
    public void IsAllowed_ValidProfileImagePath_ReturnsTrue(string path)
    {
        Assert.True(ProfileImageStaticPathPolicy.IsAllowed(path));
    }

    [Theory]
    [InlineData($"/lawyers/{LawyerId}/documents/private.jpg")]
    [InlineData($"/lawyers/{LawyerId}/profile/private.pdf")]
    [InlineData($"/lawyers/{LawyerId}/profile/malware.exe")]
    [InlineData("/lawyers/not-a-guid/profile/image.jpg")]
    [InlineData("/lawyers/00000000000000000000000000000000/profile/image.jpg")]
    [InlineData($"/lawyers/{LawyerId}/profile/../image.jpg")]
    [InlineData($"/lawyers/{LawyerId}/profile/%2e%2e.jpg")]
    [InlineData($"/lawyers/{LawyerId}/profile/folder\\image.jpg")]
    [InlineData($"/lawyers/{LawyerId}/profile/folder/image.jpg")]
    [InlineData($"/lawyers/{LawyerId}/profile/.hidden.jpg")]
    public void IsAllowed_PrivateOrUnsafePath_ReturnsFalse(string path)
    {
        Assert.False(ProfileImageStaticPathPolicy.IsAllowed(path));
    }
}
