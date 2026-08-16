using System.Linq.Expressions;

namespace LawyerPlatform.Application.Abstractions.Lawyers;

public interface IConcurrencyTokenManager
{
    void SetOriginalRowVersion<TEntity>(TEntity entity, byte[] rowVersion)
        where TEntity : class;

    void MarkPropertyModified<TEntity, TProperty>(
        TEntity entity,
        Expression<Func<TEntity, TProperty>> propertyExpression)
        where TEntity : class;
}
