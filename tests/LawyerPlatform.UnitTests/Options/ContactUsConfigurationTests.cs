using LawyerPlatform.Application.Abstractions.ContactInquiries;
using LawyerPlatform.Infrastructure;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.UnitTests.Configuration;

public sealed class ContactUsConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void MissingOrInvalidSupportEmailFailsOptionsValidation(string? supportEmail)
    {
        using var provider = CreateProvider(supportEmail);

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ContactUsOptions>>().Value);
    }

    [Fact]
    public void ValidSupportEmailResolvesRecipientProvider()
    {
        using var provider = CreateProvider("support@example.test");

        var recipient = provider.GetRequiredService<IContactUsRecipientProvider>();

        Assert.Equal("support@example.test", recipient.SupportEmail);
    }

    private static ServiceProvider CreateProvider(string? supportEmail)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] =
                "Server=(localdb)\\mssqllocaldb;Database=LawyerPlatformContactOptions;Trusted_Connection=True;"
        };
        if (supportEmail is not null)
        {
            values["ContactUs:SupportEmail"] = supportEmail;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLawyerPlatformInfrastructure(configuration);

        return services.BuildServiceProvider();
    }
}
