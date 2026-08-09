namespace LawyerPlatform.Application.Abstractions.Consultations;

public interface IConsultationAggregatePersistence
{
    void Add<TEntity>(TEntity entity) where TEntity : class;
}
