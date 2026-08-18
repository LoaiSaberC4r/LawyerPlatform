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
using LawyerPlatform.Application.Notifications.Email;

namespace LawyerPlatform.Application.Features.ConsultationRequests.Create;

public sealed record CreateConsultationRequestResponse(
    Guid Id,
    string ReferenceNumber,
    string ConsultationType,
    decimal? ConsultationPrice,
    string Status,
    DateTime CreatedOnUtc);

internal sealed class ConsultationRequestCreationService(
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> lawyerReader,
    IReadRepository<LegalSpecialization, LawyerPlatformReadPersistence> specializationReader,
    IReadRepository<ConsultationRequest, LawyerPlatformReadPersistence> requestReader,
    IConsultationReferenceNumberGenerator referenceNumberGenerator,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    ILawyerDocumentPolicy documentPolicy,
    IConsultationSchedulingTimeZone schedulingTimeZone,
    EmailNotificationCoordinator emailNotifications,
    IDateTimeProvider clock)
{
    public async Task<Result<CreateConsultationRequestResponse>> CreateGuestAsync(
        Guid lawyerId,
        ConsultationType consultationType,
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
            consultationType,
            legalSpecializationId,
            fullName,
            phoneNumber,
            email,
            description,
            preferredAppointmentOnUtc,
            null,
            null,
            null,
            cancellationToken);

    public async Task<Result<CreateConsultationRequestResponse>> CreateClientAsync(
        Guid clientProfileId,
        Guid lawyerId,
        ConsultationType consultationType,
        int? legalSpecializationId,
        string requesterName,
        string requesterEmail,
        string description,
        DateTime? preferredAppointmentOnUtc,
        CancellationToken cancellationToken)
        => await CreateAsync(
            clientProfileId,
            lawyerId,
            consultationType,
            legalSpecializationId,
            null,
            null,
            null,
            description,
            preferredAppointmentOnUtc,
            requesterName,
            requesterEmail,
            clientProfileId.ToString("N"),
            cancellationToken);

    private async Task<Result<CreateConsultationRequestResponse>> CreateAsync(
        Guid? clientProfileId,
        Guid lawyerId,
        ConsultationType consultationType,
        int? legalSpecializationId,
        string? guestFullName,
        string? guestPhoneNumber,
        string? guestEmail,
        string description,
        DateTime? preferredAppointmentOnUtc,
        string? persistedRequesterName,
        string? persistedRequesterEmail,
        string? requesterIdentity,
        CancellationToken cancellationToken)
    {
        var specializationNameAr = "غير محدد";
        var specializationNameEn = "Not specified";
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

            specializationNameAr = specialization.NameAr;
            specializationNameEn = specialization.NameEn;
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

        if (consultationType == ConsultationType.Online && !lawyer.HasConsultationSettings)
        {
            return Result<CreateConsultationRequestResponse>.Fail(
                ConsultationRequestErrors.LawyerAvailabilityNotConfigured);
        }

        if (preferredAppointmentOnUtc is { } appointmentOnUtc)
        {
            if (!lawyer.HasConsultationSettings ||
                !lawyer.Availability.Any(period => period.ConsultationType == consultationType))
            {
                return Result<CreateConsultationRequestResponse>.Fail(
                    ConsultationRequestErrors.LawyerAvailabilityNotConfigured);
            }

            var localAppointment = schedulingTimeZone.ConvertUtcToBusinessLocal(appointmentOnUtc);
            var availability = lawyer.Availability.SingleOrDefault(
                period => period.ConsultationType == consultationType &&
                          period.DayOfWeek == localAppointment.DayOfWeek);
            if (availability is null)
            {
                return Result<CreateConsultationRequestResponse>.Fail(
                    ConsultationRequestErrors.LawyerNotAvailableOnSelectedDay);
            }

            var localTime = TimeOnly.FromDateTime(localAppointment);
            if (localTime < availability.StartTime || localTime > availability.EndTime)
            {
                return Result<CreateConsultationRequestResponse>.Fail(
                    ConsultationRequestErrors.OutsideLawyerWorkingHours);
            }
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
                consultationType,
                legalSpecializationId,
                description,
                preferredAppointmentOnUtc,
                nowUtc,
                consultationType == ConsultationType.Online
                    ? lawyer.OnlineConsultationPrice
                    : null)
            : ConsultationRequest.CreateForGuest(
                referenceNumber,
                guestFullName!,
                guestPhoneNumber!,
                guestEmail,
                lawyerId,
                consultationType,
                legalSpecializationId,
                description,
                preferredAppointmentOnUtc,
                nowUtc,
                consultationType == ConsultationType.Online
                    ? lawyer.OnlineConsultationPrice
                    : null);
        if (creation.IsFailure)
        {
            return Result<CreateConsultationRequestResponse>.Fail(creation.Errors);
        }

        var request = creation.Value;
        await unitOfWork.WriteRepository<ConsultationRequest>().AddAsync(request, cancellationToken);
        await emailNotifications.QueueConsultationCreatedAsync(
            request,
            new ConsultationCreationEmailContext(
                lawyer.UserAccountId,
                lawyer.FullName,
                lawyer.Email,
                persistedRequesterName ?? request.GuestFullName!,
                persistedRequesterEmail ?? request.GuestEmail,
                requesterIdentity ?? request.Id.ToString("N"),
                specializationNameAr,
                specializationNameEn),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<CreateConsultationRequestResponse>.Ok(new CreateConsultationRequestResponse(
            request.Id,
            request.ReferenceNumber,
            request.ConsultationType.ToString(),
            request.ConsultationPrice,
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
    Guid UserAccountId,
    string FullName,
    string Email,
    IReadOnlyList<int> ActiveSpecializationIds,
    bool HasConsultationSettings,
    decimal? OnlineConsultationPrice,
    IReadOnlyList<EligibleLawyerAvailabilitySnapshot> Availability);

internal sealed record EligibleLawyerAvailabilitySnapshot(
    ConsultationType ConsultationType,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

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
            profile.UserAccountId,
            profile.FullName,
            profile.UserAccount.Email,
            profile.Specializations
                .Where(item => item.LegalSpecialization.IsActive)
                .Select(item => item.LegalSpecializationId)
                .ToArray(),
            profile.ConsultationSettings != null,
            profile.ConsultationSettings == null
                ? null
                : profile.ConsultationSettings.OnlineConsultationPrice,
            profile.ConsultationSettings == null
                ? Array.Empty<EligibleLawyerAvailabilitySnapshot>()
                : profile.ConsultationSettings.Availability
                    .Select(item => new EligibleLawyerAvailabilitySnapshot(
                        item.ConsultationType,
                        item.DayOfWeek,
                        item.StartTime,
                        item.EndTime))
                    .ToArray()));
    }
}

internal sealed record ConsultationSpecializationSnapshot(
    int Id,
    bool IsActive,
    string NameAr,
    string NameEn);

internal sealed class ConsultationSpecializationByIdSpecification
    : Specification<LegalSpecialization, ConsultationSpecializationSnapshot>
{
    public ConsultationSpecializationByIdSpecification(int id)
    {
        AddCriteria(item => item.Id == id);
        UseNoTracking();
        Select(item => new ConsultationSpecializationSnapshot(
            item.Id,
            item.IsActive,
            item.NameAr,
            item.NameEn));
    }
}
