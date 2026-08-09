using System.Net.Mail;
using BuildingBlock.Application.Exceptions;
using BuildingBlock.Infrastructure.Bootstrap;
using BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Application.Abstractions.Consultations;
using LawyerPlatform.Application.Abstractions.Seeding;
using LawyerPlatform.Application.Features.Auth.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Infrastructure.Authentication;
using LawyerPlatform.Infrastructure.Consultations;
using LawyerPlatform.Infrastructure.Options;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Seeding;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Infrastructure.Lawyers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LawyerPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLawyerPlatformInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");

        var migrationsAssemblyName = typeof(LawyerPlatformDbContext)
            .Assembly
            .GetName()
            .Name
            ?? throw new InvalidOperationException("Unable to resolve the migrations assembly name.");

        services.AddSingleton<IExceptionToErrorMapper, LawyerPlatformUniqueConstraintExceptionMapper>();
        services.AddBuildingBlockEntityFrameworkCore<LawyerPlatformWritePersistence>();
        services.AddBuildingBlockInterceptors();
        services.AddBuildingBlockCaching();
        services.AddBuildingBlockPasswordHashing(configuration);
        services.AddBuildingBlockMedia(configuration);
        services.AddBuildingBlockFileSystemMediaStorage();
        services.AddBuildingBlockSqlServerExceptionMapping();

        services.AddOptions<InitialSuperAdminOptions>()
            .Bind(configuration.GetSection(InitialSuperAdminOptions.SectionName))
            .Validate(ValidateInitialSuperAdmin, "Initial SuperAdmin options are invalid.")
            .ValidateOnStart();
        services.AddOptions<PasswordLifecycleOptions>()
            .Bind(configuration.GetSection(PasswordLifecycleOptions.SectionName))
            .Validate(options => options.ExpiryDays > 0, "Password expiry days must be greater than zero.")
            .ValidateOnStart();
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(ValidateJwt, "JWT options are invalid.")
            .ValidateOnStart();
        services.AddOptions<DatabaseInitializationOptions>()
            .Bind(configuration.GetSection(DatabaseInitializationOptions.SectionName));
        services.AddOptions<LawyerDocumentOptions>()
            .Bind(configuration.GetSection(LawyerDocumentOptions.SectionName))
            .Validate(ValidateLawyerDocuments, "Lawyer document options are invalid.")
            .ValidateOnStart();

        services.AddSingleton<IAccountIdentifierNormalizer, AccountIdentifierNormalizer>();
        services.AddSingleton<IPasswordLifecycleService, PasswordLifecycleService>();
        services.AddSingleton<IJwtProvider, JwtProvider>();
        services.AddSingleton<ILawyerDocumentPolicy, LawyerDocumentPolicy>();
        services.AddSingleton<IConsultationReferenceNumberGenerator, ConsultationReferenceNumberGenerator>();
        services.AddScoped<IConsultationAggregatePersistence, ConsultationAggregatePersistence>();
        services.AddSingleton<IStoredFileReader, StoredFileReader>();
        services.AddScoped<IConcurrencyTokenManager, ConcurrencyTokenManager>();
        services.AddScoped<ILawyerAggregatePersistence, LawyerAggregatePersistence>();

        services.AddScoped<EgyptLocationSeedCoordinator>();
        services.AddScoped<ISeeder, SuperAdminSeeder>();
        services.AddScoped<ISeeder, GovernorateSeeder>();
        services.AddScoped<ISeeder, CitySeeder>();
        services.AddScoped<ISeeder, AreaSeeder>();
        services.AddScoped<ISeeder, LegalSpecializationSeeder>();
        services.AddScoped<IEnsureSeeding, EnsureSeeding>();
        services.AddSingleton<IDatabaseMigrationService, EfCoreDatabaseMigrationService>();
        services.AddHostedService<DatabaseInitializationHostedService>();

        services.AddDbContext<LawyerPlatformDbContext>((serviceProvider, options) =>
            options
                .UseSqlServer(connectionString, sql =>
                    sql.MigrationsAssembly(migrationsAssemblyName))
                .UseBuildingBlockInterceptors(serviceProvider));

        services.AddBuildingBlockDbContext<LawyerPlatformReadPersistence, LawyerPlatformDbContext>();
        services.AddBuildingBlockDbContext<LawyerPlatformWritePersistence, LawyerPlatformDbContext>();

        return services;
    }

    private static bool ValidateInitialSuperAdmin(InitialSuperAdminOptions options)
        => !options.Enabled ||
           options.Id != Guid.Empty &&
           UserNameRules.IsValid(options.UserName) &&
           MailAddress.TryCreate(options.Email, out _) &&
           !string.IsNullOrWhiteSpace(options.PhoneNumber) &&
           !string.IsNullOrWhiteSpace(options.Password);

    private static bool ValidateJwt(JwtOptions options)
        => !string.IsNullOrWhiteSpace(options.Issuer) &&
           !string.IsNullOrWhiteSpace(options.Audience) &&
           options.Key.Length >= 64 &&
           options.AccessTokenExpirationMinutes > 0;

    private static bool ValidateLawyerDocuments(LawyerDocumentOptions options)
        => options.MaximumFileSizeBytes > 0 &&
           options.RequiredDocumentTypes.All(value => !string.IsNullOrWhiteSpace(value)) &&
           options.RequiredDocumentTypes.Distinct(StringComparer.OrdinalIgnoreCase).Count() == options.RequiredDocumentTypes.Length &&
           options.AllowedExtensions.Length > 0 &&
           options.AllowedExtensions.All(value => value.StartsWith('.') && !value.Contains('/') && !value.Contains('\\')) &&
           options.AllowedContentTypes.Length > 0 &&
           options.AllowedContentTypes.All(value => !string.IsNullOrWhiteSpace(value));
}
