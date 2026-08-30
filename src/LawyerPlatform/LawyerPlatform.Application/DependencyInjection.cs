using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Bootstrap;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Features.AdminLawyers.Common;
using LawyerPlatform.Application.Features.ConsultationRequests.Create;
using LawyerPlatform.Application.Features.AdminClients.Common;
using LawyerPlatform.Application.Notifications.Email;
using LawyerPlatform.Application.Common.Validation;

namespace LawyerPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddLawyerPlatformApplication(this IServiceCollection services)
    {
        ValidatorOptions.Global.LanguageManager = new ErrorMessageLanguageManager();

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
        services.AddScoped<EmailNotificationCoordinator>();
        services.AddSingleton<IEmailNotificationFactory, BilingualEmailNotificationFactory>();
        services.AddSingleton<IPasswordResetOtpEmailFactory, PasswordResetOtpEmailFactory>();

        return services;
    }
}
