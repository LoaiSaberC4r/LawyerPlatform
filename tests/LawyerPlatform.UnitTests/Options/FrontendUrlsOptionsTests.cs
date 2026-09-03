using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.UnitTests;

public sealed class FrontendUrlsOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("consultation/track")]
    [InlineData("ftp://frontend.example.test/consultation/track")]
    [InlineData("https://user:secret@frontend.example.test/consultation/track")]
    [InlineData("https://frontend.example.test/consultation/track?reference=AV-234567")]
    [InlineData("https://frontend.example.test/consultation/track#secret")]
    public void ValidatorRejectsMissingUnsafeOrCredentialBearingTrackingUrls(string? value)
    {
        var result = Validate(value, Environments.Development);

        Assert.True(result.Failed);
    }

    [Fact]
    public void ValidatorRequiresHttpsInProduction()
    {
        Assert.True(Validate(
            "http://frontend.example.test/consultation/track",
            Environments.Production).Failed);
        Assert.True(Validate(
            "https://frontend.example.test/consultation/track",
            Environments.Production).Succeeded);
    }

    [Fact]
    public void ValidatorAllowsHttpForLocalDevelopment()
    {
        Assert.True(Validate(
            "http://localhost:4200/consultation/track",
            Environments.Development).Succeeded);
    }

    private static ValidateOptionsResult Validate(string? value, string environmentName)
        => new FrontendUrlsOptionsValidator(new TestHostEnvironment(environmentName)).Validate(
            Options.DefaultName,
            new FrontendUrlsOptions { ConsultationTrackingUrl = value! });

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "LawyerPlatform.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
