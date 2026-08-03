using BuildingBlock.Domain.Primitive;

namespace LawyerPlatform.Domain.Catalog;

public sealed record CatalogItemCreatedDomainEvent(Guid CatalogItemId) : IDomainEvent;
