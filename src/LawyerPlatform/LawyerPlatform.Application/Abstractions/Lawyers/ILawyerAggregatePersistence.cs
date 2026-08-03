namespace LawyerPlatform.Application.Abstractions.Lawyers;

public interface ILawyerAggregatePersistence
{
    void Add<TEntity>(TEntity entity) where TEntity : class;
    void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;
    void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;
}
