using LawyerPlatform.Infrastructure.Media;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.UnitTests.Media;

public sealed class ProfileImagePathResolverTests
{
    private const string LawyerId = "945e8e5016984202b634590e5477d495";
    private const string FileName = "abc123.png";

    private readonly ProfileImagePathResolver _resolver = new(
        Options.Create(new ProfileImagesOptions { PublicPathBase = "/uploads" }));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_NullOrEmptyKey_ReturnsNull(string? storageKey)
    {
        Assert.Null(_resolver.Resolve(storageKey));
    }

    [Fact]
    public void Resolve_WindowsSeparators_NormalizesToForwardSlashes()
    {
        var result = Assert.IsType<string>(
            _resolver.Resolve($"lawyers\\{LawyerId}\\profile\\{FileName}"));

        Assert.Equal($"/uploads/lawyers/{LawyerId}/profile/{FileName}", result);
        Assert.DoesNotContain("\\", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_LinuxSeparators_ReturnsRootRelativePublicPath()
    {
        var result = Assert.IsType<string>(
            _resolver.Resolve($"lawyers/{LawyerId}/profile/{FileName}"));

        Assert.Equal($"/uploads/lawyers/{LawyerId}/profile/{FileName}", result);
        Assert.StartsWith("/", result, StringComparison.Ordinal);
        Assert.DoesNotContain("//", result, StringComparison.Ordinal);
        Assert.DoesNotContain("App_Data", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":\\", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../lawyers/945e8e5016984202b634590e5477d495/profile/abc123.png")]
    [InlineData("lawyers/945e8e5016984202b634590e5477d495/../abc123.png")]
    [InlineData("lawyers/945e8e5016984202b634590e5477d495/profile/../abc123.png")]
    public void Resolve_Traversal_RejectsRootEscape(string storageKey)
    {
        Assert.Throws<ArgumentException>(() => _resolver.Resolve(storageKey));
    }

    [Theory]
    [InlineData("C:\\wwwroot\\uploads\\lawyers\\945e8e5016984202b634590e5477d495\\profile\\abc123.png")]
    [InlineData("/var/www/uploads/lawyers/945e8e5016984202b634590e5477d495/profile/abc123.png")]
    [InlineData("\\\\server\\share\\lawyers\\945e8e5016984202b634590e5477d495\\profile\\abc123.png")]
    public void Resolve_PhysicalPath_RejectsInsteadOfExposingIt(string storageKey)
    {
        Assert.Throws<ArgumentException>(() => _resolver.Resolve(storageKey));
    }

    [Theory]
    [InlineData("lawyers//945e8e5016984202b634590e5477d495/profile/abc123.png")]
    [InlineData("lawyers/945e8e5016984202b634590e5477d495/profile//abc123.png")]
    public void Resolve_DoubleSlash_RejectsMalformedKey(string storageKey)
    {
        Assert.Throws<ArgumentException>(() => _resolver.Resolve(storageKey));
    }

    [Theory]
    [InlineData("lawyers/945e8e50-1698-4202-b634-590e5477d495/profile/abc123.png")]
    [InlineData("lawyers/945e8e5016984202b634590e5477d495/documents/abc123.png")]
    [InlineData("lawyers/945e8e5016984202b634590e5477d495/profile/abc123.pdf")]
    public void Resolve_NonProfileImageKey_RejectsIt(string storageKey)
    {
        Assert.Throws<ArgumentException>(() => _resolver.Resolve(storageKey));
    }
}
