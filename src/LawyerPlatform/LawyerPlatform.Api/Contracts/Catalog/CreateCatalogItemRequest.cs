namespace LawyerPlatform.Api.Contracts.Catalog;

public sealed record CreateCatalogItemRequest(string Name, string? Description, decimal Price);
