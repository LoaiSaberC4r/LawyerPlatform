using LawyerPlatform.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.IntegrationTests;

public sealed class CorsConfigurationTests
{
    [Fact]
    public async Task WildcardWithCredentials_FailsDuringStartup()
    {
        var configuration = CreateCorsConfiguration(allowAnyOrigin: true, allowCredentials: true);

        await AssertStartupValidationFailure(configuration);
    }

    [Fact]
    public async Task ProductionWildcard_FailsDuringStartup()
    {
        var configuration = CreateCorsConfiguration(allowAnyOrigin: true, allowCredentials: false);

        await AssertStartupValidationFailure(configuration, Environments.Production);
    }

    [Fact]
    public async Task MissingExplicitOrigins_FailsDuringStartup()
    {
        var configuration = CreateCorsConfiguration(allowAnyOrigin: false, allowCredentials: false);

        await AssertStartupValidationFailure(configuration);
    }

    [Theory]
    [InlineData("http://localhost:4200/")]
    [InlineData("https://example.com/app")]
    [InlineData("https://example.com?x=1")]
    [InlineData("https://example.com#section")]
    [InlineData("https://user@example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("localhost:4200")]
    [InlineData("*")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidExplicitOrigin_FailsDuringStartup(string origin)
    {
        var configuration = CreateCorsConfiguration(false, false, origin);

        await AssertStartupValidationFailure(configuration);
    }

    [Fact]
    public async Task CaseInsensitiveDuplicateOrigins_FailDuringStartup()
    {
        var configuration = CreateCorsConfiguration(
            false,
            false,
            "https://LawyerPlatform.example",
            "https://lawyerplatform.example");

        await AssertStartupValidationFailure(configuration);
    }

    [Fact]
    public async Task WildcardWithExplicitOrigins_FailsDuringStartup()
    {
        var configuration = CreateCorsConfiguration(true, false, "http://localhost:4200");

        await AssertStartupValidationFailure(configuration);
    }

    [Fact]
    public async Task ValidExplicitOrigin_StartsSuccessfully()
    {
        var configuration = CreateCorsConfiguration(false, false, "http://localhost:4200");

        await StartHost(configuration, Environments.Development);
    }

    private static async Task AssertStartupValidationFailure(
        Dictionary<string, string?> configuration,
        string environment = "Development")
    {
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => StartHost(configuration, environment));

        Assert.NotEmpty(exception.Failures);
    }

    private static async Task StartHost(
        Dictionary<string, string?> configuration,
        string environment)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environment
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddLawyerPlatformCors(builder.Configuration);

        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static Dictionary<string, string?> CreateCorsConfiguration(
        bool allowAnyOrigin,
        bool allowCredentials,
        params string[] allowedOrigins)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["Cors:AllowAnyOrigin"] = allowAnyOrigin.ToString(),
            ["Cors:AllowCredentials"] = allowCredentials.ToString()
        };

        for (var index = 0; index < allowedOrigins.Length; index++)
        {
            configuration[$"Cors:AllowedOrigins:{index}"] = allowedOrigins[index];
        }

        return configuration;
    }
}
