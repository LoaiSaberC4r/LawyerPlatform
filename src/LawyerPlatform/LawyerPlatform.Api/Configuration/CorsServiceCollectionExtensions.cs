using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Api.Configuration;

public static class CorsServiceCollectionExtensions
{
    public static IServiceCollection AddLawyerPlatformCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<LawyerPlatformCorsOptions>()
            .Bind(configuration.GetSection(LawyerPlatformCorsOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<LawyerPlatformCorsOptions>, LawyerPlatformCorsOptionsValidator>();
        services.AddCors();
        services.AddSingleton<IConfigureOptions<CorsOptions>, LawyerPlatformCorsPolicyConfigurator>();

        return services;
    }
}

internal sealed class LawyerPlatformCorsPolicyConfigurator : IConfigureOptions<CorsOptions>
{
    private readonly IOptions<LawyerPlatformCorsOptions> _configuredOptions;

    public LawyerPlatformCorsPolicyConfigurator(IOptions<LawyerPlatformCorsOptions> configuredOptions)
    {
        ArgumentNullException.ThrowIfNull(configuredOptions);
        _configuredOptions = configuredOptions;
    }

    public void Configure(CorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configuredOptions = _configuredOptions.Value;
        var allowedOrigins = (configuredOptions.AllowedOrigins ?? Array.Empty<string>())
            .Select(origin => origin.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        options.AddPolicy(CorsPolicyNames.Default, policy =>
        {
            policy.AllowAnyHeader();
            policy.AllowAnyMethod();

            if (configuredOptions.AllowAnyOrigin)
            {
                policy.AllowAnyOrigin();
            }
            else
            {
                policy.WithOrigins(allowedOrigins);
            }

            if (configuredOptions.AllowCredentials)
            {
                policy.AllowCredentials();
            }
        });
    }
}
