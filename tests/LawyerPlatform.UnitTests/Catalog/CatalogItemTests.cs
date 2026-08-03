using LawyerPlatform.Domain.Catalog;

namespace LawyerPlatform.UnitTests.Catalog;

public sealed class CatalogItemTests
{
    [Fact]
    public void Create_WithValidValues_ReturnsSuccessfulResult()
    {
        var result = CatalogItem.Create("Sample", "Description", 10m);

        Assert.True(result.IsSuccess);
        Assert.Equal("Sample", result.Value.Name);
        Assert.Single(result.Value.DomainEvents);
    }

    [Fact]
    public void Create_WithNegativePrice_ReturnsDomainFailure()
    {
        var result = CatalogItem.Create("Sample", null, -1m);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "CatalogItems.PriceInvalid");
    }
}
