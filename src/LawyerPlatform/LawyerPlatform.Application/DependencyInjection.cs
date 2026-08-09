using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Bootstrap;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using LawyerPlatform.Application.Catalog.DomainEvents;
using LawyerPlatform.Domain.Catalog;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Features.AdminLawyers.Common;
using LawyerPlatform.Application.Features.ConsultationRequests.Create;

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
        services.AddScoped<LawyerAggregateCompletionService>();
        services.AddScoped<AdminLawyerDecisionService>();
        services.AddScoped<ConsultationRequestCreationService>();

        return services;
    }
}
