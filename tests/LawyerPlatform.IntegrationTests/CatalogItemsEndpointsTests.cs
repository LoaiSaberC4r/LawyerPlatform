using System.Net;
using System.Net.Http.Json;

namespace LawyerPlatform.IntegrationTests;

public sealed class CatalogItemsEndpointsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Create_Then_GetById_ReturnsCreatedItem()
    {
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/catalog-items",
            new { name = "Integration Item", description = "Created by test", price = 25m },
            cancellationToken);

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CatalogItemDto>(cancellationToken);
        Assert.NotNull(created);

        var getResponse = await client.GetAsync($"/api/v1/catalog-items/{created.Id}", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var loaded = await getResponse.Content.ReadFromJsonAsync<CatalogItemDto>(cancellationToken);
        Assert.Equal("Integration Item", loaded?.Name);
    }

    private sealed record CatalogItemDto(Guid Id, string Name, string? Description, decimal Price);
}
