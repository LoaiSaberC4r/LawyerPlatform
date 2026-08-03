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

        var createResponse = await client.PostAsJsonAsync(
            "/api/catalog-items",
            new { name = "Integration Item", description = "Created by test", price = 25m });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CatalogItemDto>();
        Assert.NotNull(created);

        var getResponse = await client.GetAsync($"/api/catalog-items/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var loaded = await getResponse.Content.ReadFromJsonAsync<CatalogItemDto>();
        Assert.Equal("Integration Item", loaded?.Name);
    }

    private sealed record CatalogItemDto(Guid Id, string Name, string? Description, decimal Price);
}
