using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using FluentValidation;
using LawyerPlatform.Application.Common.Validation;
using LawyerPlatform.Application.Features.ConsultationRequests.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Application.Features.ConsultationRequests.Guest.TrackRequest;

public sealed record TrackGuestConsultationRequestQuery(string ReferenceNumber, string PhoneNumber)
    : IQuery<GuestConsultationTrackingResponse>;

public sealed record GuestConsultationTrackingResponse(
    string ReferenceNumber,
    ConsultationLawyerSummaryResponse Lawyer,
    ConsultationReferenceSummaryResponse? LegalSpecialization,
    string Status,
    DateTime? PreferredAppointmentOnUtc,
    decimal? ConsultationPrice,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    DateTime? CompletedOnUtc);

internal sealed class TrackGuestConsultationRequestQueryValidator
    : AbstractValidator<TrackGuestConsultationRequestQuery>
{
    public TrackGuestConsultationRequestQueryValidator()
    {
        RuleFor(query => query.ReferenceNumber).NotEmpty().MaximumLength(ConsultationRequest.MaximumReferenceNumberLength);
        RuleFor(query => query.PhoneNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("ConsultationRequest.PhoneNumberRequired")
            .EgyptianMobileNumber().WithErrorCode("ConsultationRequest.InvalidPhoneNumber");
    }
}

internal sealed class TrackGuestConsultationRequestQueryHandler(
    IReadRepository<ConsultationRequest, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<TrackGuestConsultationRequestQuery, GuestConsultationTrackingResponse>
{
    public async Task<Result<GuestConsultationTrackingResponse>> Handle(
        TrackGuestConsultationRequestQuery query,
        CancellationToken cancellationToken)
    {
        var item = await repository.FirstOrDefaultAsync(
            new GuestConsultationTrackingSpecification(
                query.ReferenceNumber.Trim().ToUpperInvariant(),
                query.PhoneNumber.Trim()),
            cancellationToken);
        return item is null
            ? Result<GuestConsultationTrackingResponse>.Fail(
                ConsultationRequestErrors.ReferenceVerificationFailed)
            : Result<GuestConsultationTrackingResponse>.Ok(item.ToResponse());
    }
}

internal sealed record GuestConsultationTrackingSnapshot(
    string ReferenceNumber,
    Guid LawyerId,
    string LawyerFullName,
    string? LawyerProfessionalTitle,
    int? SpecializationId,
    string? SpecializationNameAr,
    string? SpecializationNameEn,
    ConsultationRequestStatus Status,
    DateTime? PreferredAppointmentOnUtc,
    decimal? ConsultationPrice,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    DateTime? CompletedOnUtc)
{
    public GuestConsultationTrackingResponse ToResponse()
        => new(
            ReferenceNumber,
            new ConsultationLawyerSummaryResponse(LawyerId, LawyerFullName, LawyerProfessionalTitle),
            SpecializationId.HasValue
                ? new ConsultationReferenceSummaryResponse(
                    SpecializationId.Value,
                    SpecializationNameAr!,
                    SpecializationNameEn!)
                : null,
            Status.ToString(),
            PreferredAppointmentOnUtc,
            ConsultationPrice,
            CreatedOnUtc,
            ModifiedOnUtc,
            CompletedOnUtc);
}

internal sealed class GuestConsultationTrackingSpecification
    : Specification<ConsultationRequest, GuestConsultationTrackingSnapshot>
{
    public GuestConsultationTrackingSpecification(string referenceNumber, string phoneNumber)
    {
        AddCriteria(request =>
            request.ClientProfileId == null &&
            request.ReferenceNumber == referenceNumber &&
            request.GuestPhoneNumber == phoneNumber);
        UseNoTracking();
        Select(request => new GuestConsultationTrackingSnapshot(
            request.ReferenceNumber,
            request.LawyerProfileId,
            request.LawyerProfile.FullName,
            request.LawyerProfile.ProfessionalTitle,
            request.LegalSpecializationId,
            request.LegalSpecialization != null ? request.LegalSpecialization.NameAr : null,
            request.LegalSpecialization != null ? request.LegalSpecialization.NameEn : null,
            request.Status,
            request.PreferredAppointmentOnUtc,
            request.ConsultationPrice,
            request.CreatedOnUtc,
            request.ModifiedOnUtc,
            request.CompletedOnUtc));
    }
}
