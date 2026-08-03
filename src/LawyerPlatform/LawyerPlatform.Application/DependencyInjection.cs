using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Bootstrap;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using LawyerPlatform.Application.Catalog.DomainEvents;
using LawyerPlatform.Domain.Catalog;

namespace LawyerPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddLawyerPlatformApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(AssemblyReference.Assembly));

        services.AddValidatorsFromAssembly(
            AssemblyReference.Assembly,
            includeInternalTypes: true);

        services.AddBuildingBlockApplicationBehaviors();
        services.AddScoped<IDomainEventHandler<CatalogItemCreatedDomainEvent>, CatalogItemCreatedDomainEventHandler>();

        return services;
    }
}
