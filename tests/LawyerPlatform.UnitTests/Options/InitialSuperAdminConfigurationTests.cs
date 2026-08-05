using LawyerPlatform.Infrastructure;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.UnitTests.Configuration;

public sealed class InitialSuperAdminConfigurationTests
{
    [Fact]
    public void EnabledBindsTrueFromInitialSuperAdminSection()
    {
        var options = BindOptions("true");

        Assert.True(options.Enabled);
    }

    [Fact]
    public void EnabledBindsFalseWhenExplicitlyConfiguredFalse()
    {
        var options = BindOptions("false");

        Assert.False(options.Enabled);
    }

    [Fact]
    public void InfrastructureRegistersOneBindingWithoutResettingEnabled()
    {
        var configuration = CreateConfiguration("true");
        var services = new ServiceCollection();

        services.AddLawyerPlatformInfrastructure(configuration);

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IConfigureOptions<InitialSuperAdminOptions>));

        using var provider = services.BuildServiceProvider();
        var configured = provider
            .GetRequiredService<IOptions<InitialSuperAdminOptions>>()
            .Value;

        Assert.True(configured.Enabled);
    }

    private static InitialSuperAdminOptions BindOptions(string enabled)
    {
        var configuration = CreateConfiguration(enabled);
        var services = new ServiceCollection();
        services.AddOptions<InitialSuperAdminOptions>()
            .Bind(configuration.GetSection(InitialSuperAdminOptions.SectionName));

        using var provider = services.BuildServiceProvider();
        return provider
            .GetRequiredService<IOptions<InitialSuperAdminOptions>>()
            .Value;
    }

    private static IConfiguration CreateConfiguration(string enabled)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Server=localhost;Database=LawyerPlatformTests;Integrated Security=True;TrustServerCertificate=True",
                ["InitialSuperAdmin:Enabled"] = enabled,
                ["InitialSuperAdmin:Id"] = "11111111-1111-1111-1111-111111111111",
                ["InitialSuperAdmin:UserName"] = "superadmin",
                ["InitialSuperAdmin:Email"] = "admin@lawyerplatform.test",
                ["InitialSuperAdmin:PhoneNumber"] = "01000000000",
                ["InitialSuperAdmin:Password"] = "InitialPassword1!"
            })
            .Build();
}
