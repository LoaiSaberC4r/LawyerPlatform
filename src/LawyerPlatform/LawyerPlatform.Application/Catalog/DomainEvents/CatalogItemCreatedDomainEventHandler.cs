using BuildingBlock.Application.Abstraction;
using Microsoft.Extensions.Logging;
using LawyerPlatform.Domain.Catalog;

namespace LawyerPlatform.Application.Catalog.DomainEvents;

internal sealed partial class CatalogItemCreatedDomainEventHandler
    : IDomainEventHandler<CatalogItemCreatedDomainEvent>
{
    private readonly ILogger<CatalogItemCreatedDomainEventHandler> _logger;

    public CatalogItemCreatedDomainEventHandler(
        ILogger<CatalogItemCreatedDomainEventHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Handle(
        CatalogItemCreatedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        LogCatalogItemCreated(_logger, domainEvent.CatalogItemId);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Catalog item {CatalogItemId} was created inside the current unit of work.")]
    private static partial void LogCatalogItemCreated(
        ILogger logger,
        Guid catalogItemId);
}
