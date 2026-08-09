using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Bootstrap;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Features.AdminLawyers.Common;
using LawyerPlatform.Application.Features.ConsultationRequests.Create;
using LawyerPlatform.Application.Features.AdminClients.Common;

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
        services.AddScoped<LawyerAggregateCompletionService>();
        services.AddScoped<AdminLawyerDecisionService>();
        services.AddScoped<ConsultationRequestCreationService>();
        services.AddScoped<AdminClientLifecycleService>();

        return services;
    }
}
