using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.UnitTests;

public sealed class EmailBrandingOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("email-assets/avokatoo-email-footer.png")]
    [InlineData("not a valid URL")]
    [InlineData("ftp://cdn.example.test/avokatoo-email-footer.png")]
    public void Validator_RejectsMissingRelativeOrInvalidFooterImageUrl(string? footerImageUrl)
    {
        var result = Validate(footerImageUrl, Environments.Development);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validator_RejectsHttpFooterImageUrlInProduction()
    {
        var result = Validate(
            "http://cdn.example.test/email-assets/avokatoo-email-footer.png",
            Environments.Production);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("HTTPS", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_AllowsHttpsFooterImageUrlInProduction()
    {
        var result = Validate(
            "https://cdn.example.test/email-assets/avokatoo-email-footer.png",
            Environments.Production);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validator_AllowsHttpFooterImageUrlOutsideProduction()
    {
        var result = Validate(
            "http://localhost:5000/email-assets/avokatoo-email-footer.png",
            Environments.Development);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("https://user:password@cdn.example.test/email-assets/footer.png")]
    [InlineData("https://cdn.example.test/email-assets/footer.png?token=secret")]
    [InlineData("https://cdn.example.test/email-assets/footer.png#token")]
    public void Validator_RejectsUrlPartsThatCouldContainCredentialsOrTokens(string footerImageUrl)
    {
        var result = Validate(footerImageUrl, Environments.Development);

        Assert.True(result.Failed);
    }

    private static ValidateOptionsResult Validate(string? footerImageUrl, string environmentName)
    {
        var validator = new EmailBrandingOptionsValidator(
            new TestHostEnvironment(environmentName));

        return validator.Validate(
            Options.DefaultName,
            new EmailBrandingOptions { FooterImageUrl = footerImageUrl! });
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "LawyerPlatform.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
