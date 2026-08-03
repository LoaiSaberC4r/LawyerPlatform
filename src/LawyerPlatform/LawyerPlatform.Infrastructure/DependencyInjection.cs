using BuildingBlock.Infrastructure.Bootstrap;
using BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Infrastructure.Persistence;

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

        services.AddBuildingBlockEntityFrameworkCore<LawyerPlatformWritePersistence>();
        services.AddBuildingBlockInterceptors();
        services.AddBuildingBlockCaching();
        services.AddBuildingBlockSqlServerExceptionMapping();

        services.AddDbContext<LawyerPlatformDbContext>((serviceProvider, options) =>
            options
                .UseSqlServer(connectionString, sql =>
                    sql.MigrationsAssembly(migrationsAssemblyName))
                .UseBuildingBlockInterceptors(serviceProvider));

        services.AddBuildingBlockDbContext<LawyerPlatformReadPersistence, LawyerPlatformDbContext>();
        services.AddBuildingBlockDbContext<LawyerPlatformWritePersistence, LawyerPlatformDbContext>();

        return services;
    }
}
