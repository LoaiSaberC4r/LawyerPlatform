namespace LawyerPlatform.Application.Abstractions.Lawyers;

public interface IConcurrencyTokenManager
{
    void SetOriginalRowVersion<TEntity>(TEntity entity, byte[] rowVersion)
        where TEntity : class;

    void MarkModified<TEntity>(TEntity entity)
        where TEntity : class;
}
