using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LawyerPlatform.IntegrationTests;

public sealed class EmailBrandingStaticFileTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task FooterImage_IsServedAnonymouslyFromEmailAssets()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync(
            "/email-assets/avokatoo-email-footer.png",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.True((await response.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken)).Length > 0);
    }
}
