using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;

namespace LawyerPlatform.Infrastructure.Lawyers;

internal sealed class LawyerAggregatePersistence(LawyerPlatformDbContext dbContext) : ILawyerAggregatePersistence
{
    public void Add<TEntity>(TEntity entity) where TEntity : class => dbContext.Add(entity);
    public void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class => dbContext.AddRange(entities);
    public void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class => dbContext.RemoveRange(entities);
}
