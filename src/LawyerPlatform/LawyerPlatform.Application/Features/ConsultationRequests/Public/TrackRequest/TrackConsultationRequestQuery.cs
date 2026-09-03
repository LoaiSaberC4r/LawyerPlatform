using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using FluentValidation;
using LawyerPlatform.Application.Common.Validation;
using LawyerPlatform.Application.Features.ConsultationRequests.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Application.Features.ConsultationRequests.PublicTracking;

public sealed record TrackConsultationRequestQuery(string ReferenceNumber, string PhoneNumber)
    : IQuery<ConsultationTrackingResponse>;

public sealed record ConsultationTrackingResponse(
    string ReferenceNumber,
    ConsultationLawyerSummaryResponse Lawyer,
    ConsultationReferenceSummaryResponse? LegalSpecialization,
    string ConsultationType,
    string Status,
    DateTime? PreferredAppointmentOnUtc,
    decimal? ConsultationPrice,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    DateTime? CompletedOnUtc);

internal sealed class TrackConsultationRequestQueryValidator
    : AbstractValidator<TrackConsultationRequestQuery>
{
    public TrackConsultationRequestQueryValidator()
    {
        RuleFor(query => query.ReferenceNumber)
            .NotEmpty()
            .MaximumLength(ConsultationRequest.MaximumReferenceNumberLength);
        RuleFor(query => query.PhoneNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("ConsultationRequest.PhoneNumberRequired")
            .EgyptianMobileNumber().WithErrorCode("ConsultationRequest.InvalidPhoneNumber");
    }
}

internal sealed class TrackConsultationRequestQueryHandler(
    IReadRepository<ConsultationRequest, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<TrackConsultationRequestQuery, ConsultationTrackingResponse>
{
    public async Task<Result<ConsultationTrackingResponse>> Handle(
        TrackConsultationRequestQuery query,
        CancellationToken cancellationToken)
    {
        var item = await repository.FirstOrDefaultAsync(
            new ConsultationTrackingSpecification(
                query.ReferenceNumber.Trim().ToUpperInvariant(),
                query.PhoneNumber.Trim()),
            cancellationToken);
        return item is null
            ? Result<ConsultationTrackingResponse>.Fail(
                ConsultationRequestErrors.ReferenceVerificationFailed)
            : Result<ConsultationTrackingResponse>.Ok(item.ToResponse());
    }
}

internal sealed record ConsultationTrackingSnapshot(
    string ReferenceNumber,
    Guid LawyerId,
    string LawyerFullName,
    string? LawyerProfessionalTitle,
    int? SpecializationId,
    string? SpecializationNameAr,
    string? SpecializationNameEn,
    ConsultationType ConsultationType,
    ConsultationRequestStatus Status,
    DateTime? PreferredAppointmentOnUtc,
    decimal? ConsultationPrice,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    DateTime? CompletedOnUtc)
{
    public ConsultationTrackingResponse ToResponse()
        => new(
            ReferenceNumber,
            new ConsultationLawyerSummaryResponse(LawyerId, LawyerFullName, LawyerProfessionalTitle),
            SpecializationId.HasValue
                ? new ConsultationReferenceSummaryResponse(
                    SpecializationId.Value,
                    SpecializationNameAr!,
                    SpecializationNameEn!)
                : null,
            ConsultationType.ToString(),
            Status.ToString(),
            PreferredAppointmentOnUtc,
            ConsultationPrice,
            CreatedOnUtc,
            ModifiedOnUtc,
            CompletedOnUtc);
}

internal sealed class ConsultationTrackingSpecification
    : Specification<ConsultationRequest, ConsultationTrackingSnapshot>
{
    public ConsultationTrackingSpecification(string referenceNumber, string phoneNumber)
    {
        AddCriteria(request =>
            request.ReferenceNumber == referenceNumber &&
            (request.ClientProfileId == null
                ? request.GuestPhoneNumber == phoneNumber
                : request.ClientProfile!.UserAccount.PhoneNumber == phoneNumber));
        UseNoTracking();
        Select(request => new ConsultationTrackingSnapshot(
            request.ReferenceNumber,
            request.LawyerProfileId,
            request.LawyerProfile.FullName,
            request.LawyerProfile.ProfessionalTitle,
            request.LegalSpecializationId,
            request.LegalSpecialization != null ? request.LegalSpecialization.NameAr : null,
            request.LegalSpecialization != null ? request.LegalSpecialization.NameEn : null,
            request.ConsultationType,
            request.Status,
            request.PreferredAppointmentOnUtc,
            request.ConsultationPrice,
            request.CreatedOnUtc,
            request.ModifiedOnUtc,
            request.CompletedOnUtc));
    }
}
