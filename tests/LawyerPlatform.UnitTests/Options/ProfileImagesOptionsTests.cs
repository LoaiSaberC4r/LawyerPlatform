using BuildingBlock.Infrastructure.Options;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.UnitTests.Configuration;

public sealed class ProfileImagesOptionsTests
{
    [Fact]
    public void Validator_AcceptsMediaRootMatchingPublicDirectoryInsideWebRoot()
    {
        var environment = CreateEnvironment();
        var mediaOptions = Options.Create(new MediaStorageOptions
        {
            RootPath = Path.Combine("wwwroot", "uploads"),
            ContentRootPath = environment.ContentRootPath
        });
        var validator = new ProfileImagesOptionsValidator(environment, mediaOptions);

        var result = validator.Validate(
            null,
            new ProfileImagesOptions { PublicPathBase = "/uploads" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validator_RejectsMediaRootOutsideWebRoot()
    {
        var environment = CreateEnvironment();
        var mediaOptions = Options.Create(new MediaStorageOptions
        {
            RootPath = Path.Combine(environment.ContentRootPath, "App_Data", "Media"),
            ContentRootPath = environment.ContentRootPath
        });
        var validator = new ProfileImagesOptionsValidator(environment, mediaOptions);

        var result = validator.Validate(
            null,
            new ProfileImagesOptions { PublicPathBase = "/uploads" });

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("uploads")]
    [InlineData("/uploads/")]
    [InlineData("/../uploads")]
    [InlineData("//uploads")]
    [InlineData("/uploads?path=other")]
    public void Validator_RejectsMalformedPublicPathBase(string publicPathBase)
    {
        var environment = CreateEnvironment();
        var mediaOptions = Options.Create(new MediaStorageOptions
        {
            RootPath = Path.Combine("wwwroot", "uploads"),
            ContentRootPath = environment.ContentRootPath
        });
        var validator = new ProfileImagesOptionsValidator(environment, mediaOptions);

        var result = validator.Validate(
            null,
            new ProfileImagesOptions { PublicPathBase = publicPathBase });

        Assert.True(result.Failed);
    }

    private static TestWebHostEnvironment CreateEnvironment()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "LawyerPlatformOptionsTests", Guid.NewGuid().ToString("N"));
        return new TestWebHostEnvironment
        {
            ContentRootPath = contentRoot,
            WebRootPath = Path.Combine(contentRoot, "wwwroot")
        };
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "LawyerPlatform.UnitTests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
