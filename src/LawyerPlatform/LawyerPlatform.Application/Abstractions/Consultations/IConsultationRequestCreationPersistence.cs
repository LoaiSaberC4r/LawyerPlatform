using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Application.Abstractions.Consultations;

public interface IConsultationRequestCreationPersistence
{
    Task<bool> TryAddAsync(
        ConsultationRequest request,
        CancellationToken cancellationToken = default);
}
