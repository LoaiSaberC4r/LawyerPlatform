using System.Data;
using LawyerPlatform.Application.Abstractions.ReferenceData;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LawyerPlatform.Infrastructure.ReferenceData;

internal sealed class SqlServerReferenceDataIdGenerator(LawyerPlatformDbContext dbContext)
    : IReferenceDataIdGenerator
{
    private static int _sqliteGovernorateId = LawyerPlatformDatabaseObjectNames.GovernorateIdSequenceStart - 1;
    private static int _sqliteCityId = LawyerPlatformDatabaseObjectNames.CityIdSequenceStart - 1;
    private static int _sqliteAreaId = LawyerPlatformDatabaseObjectNames.AreaIdSequenceStart - 1;
    private static int _sqliteLegalSpecializationId = LawyerPlatformDatabaseObjectNames.LegalSpecializationIdSequenceStart - 1;

    public Task<int> NextGovernorateIdAsync(CancellationToken cancellationToken = default)
        => NextAsync(
            LawyerPlatformDatabaseObjectNames.GovernorateIdSequence,
            () => Interlocked.Increment(ref _sqliteGovernorateId),
            cancellationToken);

    public Task<int> NextCityIdAsync(CancellationToken cancellationToken = default)
        => NextAsync(
            LawyerPlatformDatabaseObjectNames.CityIdSequence,
            () => Interlocked.Increment(ref _sqliteCityId),
            cancellationToken);

    public Task<int> NextAreaIdAsync(CancellationToken cancellationToken = default)
        => NextAsync(
            LawyerPlatformDatabaseObjectNames.AreaIdSequence,
            () => Interlocked.Increment(ref _sqliteAreaId),
            cancellationToken);

    public Task<int> NextLegalSpecializationIdAsync(CancellationToken cancellationToken = default)
        => NextAsync(
            LawyerPlatformDatabaseObjectNames.LegalSpecializationIdSequence,
            () => Interlocked.Increment(ref _sqliteLegalSpecializationId),
            cancellationToken);

    private async Task<int> NextAsync(
        string sequenceName,
        Func<int> nonSqlServerFallback,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!dbContext.Database.IsSqlServer())
        {
            return nonSqlServerFallback();
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT NEXT VALUE FOR [dbo].[{sequenceName}]";
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
