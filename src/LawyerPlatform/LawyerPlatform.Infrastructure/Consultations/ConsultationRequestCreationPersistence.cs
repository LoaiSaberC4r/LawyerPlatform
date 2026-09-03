using LawyerPlatform.Application.Abstractions.Consultations;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LawyerPlatform.Infrastructure.Consultations;

internal sealed class ConsultationRequestCreationPersistence(LawyerPlatformDbContext dbContext)
    : IConsultationRequestCreationPersistence
{
    public async Task<bool> TryAddAsync(
        ConsultationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        dbContext.ConsultationRequests.Add(request);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsReferenceNumberCollision(exception))
        {
            dbContext.Entry(request).State = EntityState.Detached;
            return false;
        }
    }

    private static bool IsReferenceNumberCollision(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(
                    LawyerPlatformDatabaseObjectNames.ConsultationRequestReferenceNumberUniqueIndex,
                    StringComparison.Ordinal) ||
                current.Message.Contains(
                    "UNIQUE constraint failed: ConsultationRequests.ReferenceNumber",
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
