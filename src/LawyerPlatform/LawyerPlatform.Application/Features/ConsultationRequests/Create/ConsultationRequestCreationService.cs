using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.Consultations;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.PublicLawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.ConsultationRequests.Create;

public sealed record CreateConsultationRequestResponse(
    Guid Id,
    string ReferenceNumber,
    string Status,
    DateTime CreatedOnUtc);

internal sealed class ConsultationRequestCreationService(
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> lawyerReader,
    IReadRepository<LegalSpecialization, LawyerPlatformReadPersistence> specializationReader,
    IReadRepository<ConsultationRequest, LawyerPlatformReadPersistence> requestReader,
    IConsultationReferenceNumberGenerator referenceNumberGenerator,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    ILawyerDocumentPolicy documentPolicy,
    IDateTimeProvider clock)
{
    public async Task<Result<CreateConsultationRequestResponse>> CreateGuestAsync(
        Guid lawyerId,
        int? legalSpecializationId,
        string fullName,
        string phoneNumber,
        string? email,
        string description,
        DateTime? preferredAppointmentOnUtc,
        CancellationToken cancellationToken)
        => await CreateAsync(
            null,
            lawyerId,
            legalSpecializationId,
            fullName,
            phoneNumber,
            email,
            description,
            preferredAppointmentOnUtc,
            cancellationToken);

    public async Task<Result<CreateConsultationRequestResponse>> CreateClientAsync(
        Guid clientProfileId,
        Guid lawyerId,
        int? legalSpecializationId,
        string description,
        DateTime? preferredAppointmentOnUtc,
        CancellationToken cancellationToken)
        => await CreateAsync(
            clientProfileId,
            lawyerId,
            legalSpecializationId,
            null,
            null,
            null,
            description,
            preferredAppointmentOnUtc,
            cancellationToken);

    private async Task<Result<CreateConsultationRequestResponse>> CreateAsync(
        Guid? clientProfileId,
        Guid lawyerId,
        int? legalSpecializationId,
        string? guestFullName,
        string? guestPhoneNumber,
        string? guestEmail,
        string description,
        DateTime? preferredAppointmentOnUtc,
        CancellationToken cancellationToken)
    {
        if (legalSpecializationId is { } specializationId)
        {
            var specialization = await specializationReader.FirstOrDefaultAsync(
                new ConsultationSpecializationByIdSpecification(specializationId),
                cancellationToken);
            if (specialization is null)
            {
                return Result<CreateConsultationRequestResponse>.Fail(LegalSpecializationErrors.NotFound);
            }

            if (!specialization.IsActive)
            {
                return Result<CreateConsultationRequestResponse>.Fail(
                    LawyerPlatform.Application.Features.Lawyers.Common.LawyerApplicationErrors.SpecializationInactive);
            }

        }

        var lawyer = await lawyerReader.FirstOrDefaultAsync(
            new EligibleLawyerForConsultationSpecification(lawyerId, documentPolicy),
            cancellationToken);
        if (lawyer is null)
        {
            return Result<CreateConsultationRequestResponse>.Fail(ConsultationRequestErrors.LawyerUnavailable);
        }

        if (legalSpecializationId is { } requestedSpecializationId &&
            !lawyer.ActiveSpecializationIds.Contains(requestedSpecializationId))
        {
            return Result<CreateConsultationRequestResponse>.Fail(
                ConsultationRequestErrors.SpecializationNotOfferedByLawyer);
        }

        var nowUtc = clock.UtcNow;
        if (preferredAppointmentOnUtc is { } preferred &&
            (preferred.Kind != DateTimeKind.Utc || preferred <= nowUtc))
        {
            return Result<CreateConsultationRequestResponse>.Fail(
                ConsultationRequestErrors.PreferredAppointmentMustBeFuture);
        }

        var referenceNumber = await GenerateAvailableReferenceAsync(cancellationToken);
        if (referenceNumber is null)
        {
            return Result<CreateConsultationRequestResponse>.Fail(
                ConsultationRequestErrors.ReferenceNumberConflict);
        }

        var creation = clientProfileId is { } clientId
            ? ConsultationRequest.CreateForClient(
                referenceNumber,
                clientId,
                lawyerId,
                legalSpecializationId,
                description,
                preferredAppointmentOnUtc,
                nowUtc)
            : ConsultationRequest.CreateForGuest(
                referenceNumber,
                guestFullName!,
                guestPhoneNumber!,
                guestEmail,
                lawyerId,
                legalSpecializationId,
                description,
                preferredAppointmentOnUtc,
                nowUtc);
        if (creation.IsFailure)
        {
            return Result<CreateConsultationRequestResponse>.Fail(creation.Errors);
        }

        var request = creation.Value;
        await unitOfWork.WriteRepository<ConsultationRequest>().AddAsync(request, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<CreateConsultationRequestResponse>.Ok(new CreateConsultationRequestResponse(
            request.Id,
            request.ReferenceNumber,
            request.Status.ToString(),
            request.CreatedOnUtc));
    }

    private async Task<string?> GenerateAvailableReferenceAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = referenceNumberGenerator.Generate();
            if (!await requestReader.AnyAsync(
                    request => request.ReferenceNumber == candidate,
                    cancellationToken))
            {
                return candidate;
            }
        }

        return null;
    }
}

internal sealed record EligibleConsultationLawyerSnapshot(
    Guid Id,
    IReadOnlyList<int> ActiveSpecializationIds);

internal sealed class EligibleLawyerForConsultationSpecification
    : PublicLawyerSpecification<EligibleConsultationLawyerSnapshot>
{
    public EligibleLawyerForConsultationSpecification(Guid lawyerId, ILawyerDocumentPolicy documentPolicy)
    {
        AddCriteria(profile => profile.Id == lawyerId);
        ApplyPublicEligibility(documentPolicy);
        UseNoTracking();
        Select(profile => new EligibleConsultationLawyerSnapshot(
            profile.Id,
            profile.Specializations
                .Where(item => item.LegalSpecialization.IsActive)
                .Select(item => item.LegalSpecializationId)
                .ToArray()));
    }
}

internal sealed record ConsultationSpecializationSnapshot(int Id, bool IsActive);

internal sealed class ConsultationSpecializationByIdSpecification
    : Specification<LegalSpecialization, ConsultationSpecializationSnapshot>
{
    public ConsultationSpecializationByIdSpecification(int id)
    {
        AddCriteria(item => item.Id == id);
        UseNoTracking();
        Select(item => new ConsultationSpecializationSnapshot(item.Id, item.IsActive));
    }
}
