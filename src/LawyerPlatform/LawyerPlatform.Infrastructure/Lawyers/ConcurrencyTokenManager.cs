using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;

namespace LawyerPlatform.Infrastructure.Lawyers;

internal sealed class ConcurrencyTokenManager(LawyerPlatformDbContext dbContext) : IConcurrencyTokenManager
{
    public void SetOriginalRowVersion<TEntity>(TEntity entity, byte[] rowVersion)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(rowVersion);
        dbContext.Entry(entity).Property("RowVersion").OriginalValue = rowVersion;
    }

    public void MarkModified<TEntity>(TEntity entity)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        dbContext.Entry(entity).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
    }
}
