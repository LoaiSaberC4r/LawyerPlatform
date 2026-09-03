using BuildingBlock.Infrastructure.Options;
using LawyerPlatform.Infrastructure.Media;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.UnitTests.Configuration;

public sealed class ProfileImagesOptionsTests
{
    [Fact]
    public void Validator_AcceptsPublicPathIndependentlyFromPhysicalMediaRoot()
    {
        var validator = CreateValidator();

        var result = validator.Validate(
            null,
            new ProfileImagesOptions { PublicPathBase = "/uploads" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validator_AcceptsPrivateAppDataMediaRootConcept()
    {
        var validator = CreateValidator();

        var result = validator.Validate(
            null,
            new ProfileImagesOptions { PublicPathBase = "/uploads" });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("uploads")]
    [InlineData("/uploads/")]
    [InlineData("/../uploads")]
    [InlineData("//uploads")]
    [InlineData("/uploads?path=other")]
    [InlineData("/uploads%2fother")]
    [InlineData("/uploads\\other")]
    public void Validator_RejectsMalformedPublicPathBase(string publicPathBase)
    {
        var validator = CreateValidator();

        var result = validator.Validate(
            null,
            new ProfileImagesOptions { PublicPathBase = publicPathBase });

        Assert.True(result.Failed);
    }

    private static ProfileImagesOptionsValidator CreateValidator()
    {
        var contentRoot = Path.Combine(
            Path.GetTempPath(),
            "LawyerPlatformOptionsTests",
            Guid.NewGuid().ToString("N"));
        var resolver = new MediaStoragePathResolver(Options.Create(new MediaStorageOptions
        {
            RootPath = "App_Data/Media",
            ContentRootPath = contentRoot
        }));
        return new ProfileImagesOptionsValidator(resolver);
    }
}
