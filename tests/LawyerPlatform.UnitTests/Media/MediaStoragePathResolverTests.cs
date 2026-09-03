using BuildingBlock.Infrastructure.Options;
using LawyerPlatform.Infrastructure.Media;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.UnitTests.Media;

public sealed class MediaStoragePathResolverTests
{
    [Fact]
    public void ResolveRoot_RelativePath_UsesContentRoot()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "LawyerPlatformPathTests", Guid.NewGuid().ToString("N"));

        var result = MediaStoragePathResolver.ResolveRoot("App_Data/Media", contentRoot);

        Assert.Equal(Path.Combine(contentRoot, "App_Data", "Media"), result);
    }

    [Fact]
    public void ResolveRoot_AbsolutePath_PreservesCanonicalAbsoluteRoot()
    {
        var absoluteRoot = Path.Combine(Path.GetTempPath(), "LawyerPlatformPathTests", Guid.NewGuid().ToString("N"));

        var result = MediaStoragePathResolver.ResolveRoot(absoluteRoot, Path.Combine(Path.GetTempPath(), "other"));

        Assert.Equal(Path.GetFullPath(absoluteRoot), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" App_Data/Media")]
    [InlineData("../App_Data/Media")]
    [InlineData("App_Data/../Media")]
    [InlineData("App_Data//Media")]
    [InlineData("C:App_Data/Media")]
    public void ResolveRoot_UnsafeOrEmptyPath_RejectsIt(string configuredRoot)
    {
        Assert.Throws<ArgumentException>(() =>
            MediaStoragePathResolver.ResolveRoot(configuredRoot, Path.GetTempPath()));
    }

    [Fact]
    public void ResolveRoot_FilesystemRoot_RejectsIt()
    {
        var filesystemRoot = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()))!;

        Assert.Throws<ArgumentException>(() =>
            MediaStoragePathResolver.ResolveRoot(filesystemRoot, Path.GetTempPath()));
    }

    [Fact]
    public void TryResolveStorageKey_ValidRelativeKey_ResolvesUnderCanonicalRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "LawyerPlatformPathTests", Guid.NewGuid().ToString("N"));
        var resolver = CreateResolver(root);
        const string storageKey = "lawyers/945e8e5016984202b634590e5477d495/profile/abc123.png";

        var resolved = resolver.TryResolveStorageKey(storageKey, out var physicalPath);

        Assert.True(resolved);
        Assert.Equal(
            Path.Combine(root, "lawyers", "945e8e5016984202b634590e5477d495", "profile", "abc123.png"),
            physicalPath);
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("lawyers/945e8e5016984202b634590e5477d495/profile/../outside.png")]
    [InlineData("lawyers//945e8e5016984202b634590e5477d495/profile/file.png")]
    [InlineData("C:\\outside\\file.png")]
    [InlineData("lawyers/945e8e5016984202b634590e5477d495/profile/file%2epng")]
    public void TryResolveStorageKey_TraversalOrManipulation_RejectsIt(string storageKey)
    {
        var resolver = CreateResolver(Path.Combine(
            Path.GetTempPath(),
            "LawyerPlatformPathTests",
            Guid.NewGuid().ToString("N")));

        Assert.False(resolver.TryResolveStorageKey(storageKey, out _));
    }

    private static MediaStoragePathResolver CreateResolver(string root)
        => new(Options.Create(new MediaStorageOptions
        {
            RootPath = root,
            ContentRootPath = Path.GetTempPath()
        }));
}
