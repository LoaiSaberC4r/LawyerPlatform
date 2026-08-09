using LawyerPlatform.Application.Abstractions.Consultations;
using LawyerPlatform.Infrastructure.Persistence;

namespace LawyerPlatform.Infrastructure.Consultations;

internal sealed class ConsultationAggregatePersistence(LawyerPlatformDbContext dbContext)
    : IConsultationAggregatePersistence
{
    public void Add<TEntity>(TEntity entity) where TEntity : class => dbContext.Add(entity);
}
