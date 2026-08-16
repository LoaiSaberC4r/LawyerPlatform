using System.Linq.Expressions;
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

    public void MarkPropertyModified<TEntity, TProperty>(
        TEntity entity,
        Expression<Func<TEntity, TProperty>> propertyExpression)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(propertyExpression);
        dbContext.Entry(entity).Property(propertyExpression).IsModified = true;
    }
}
