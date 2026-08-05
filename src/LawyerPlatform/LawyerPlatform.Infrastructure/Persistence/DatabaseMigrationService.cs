using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LawyerPlatform.Infrastructure.Persistence;

internal interface IDatabaseMigrationService
{
    IReadOnlyList<string> GetCompiledMigrations(LawyerPlatformDbContext dbContext);

    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
        LawyerPlatformDbContext dbContext,
        CancellationToken cancellationToken);

    Task MigrateAsync(
        LawyerPlatformDbContext dbContext,
        CancellationToken cancellationToken);
}

internal sealed class EfCoreDatabaseMigrationService : IDatabaseMigrationService
{
    public IReadOnlyList<string> GetCompiledMigrations(LawyerPlatformDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return dbContext.Database.GetMigrations().ToArray();
    }

    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
        LawyerPlatformDbContext dbContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        var migrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        return migrations.ToArray();
    }

    public Task MigrateAsync(
        LawyerPlatformDbContext dbContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return dbContext.Database.MigrateAsync(cancellationToken);
    }
}

internal readonly record struct DatabaseConnectionDetails(
    string DataSource,
    string Database,
    bool IntegratedSecurity)
{
    public static DatabaseConnectionDetails From(LawyerPlatformDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var connection = dbContext.Database.GetDbConnection();
        if (connection is not SqlConnection sqlConnection)
        {
            return new DatabaseConnectionDetails(
                connection.DataSource,
                connection.Database,
                IntegratedSecurity: false);
        }

        var builder = new SqlConnectionStringBuilder(sqlConnection.ConnectionString);

        return new DatabaseConnectionDetails(
            builder.DataSource,
            builder.InitialCatalog,
            builder.IntegratedSecurity);
    }
}
