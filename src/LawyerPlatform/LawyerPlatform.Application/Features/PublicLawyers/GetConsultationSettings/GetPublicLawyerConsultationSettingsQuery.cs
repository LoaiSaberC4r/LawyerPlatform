using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.PublicLawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.PublicLawyers.GetConsultationSettings;

public sealed record GetPublicLawyerConsultationSettingsQuery(Guid LawyerId)
    : IQuery<PublicLawyerConsultationSettingsResponse>;

public sealed record PublicLawyerAvailabilityResponse(
    string DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed record PublicOnlineConsultationSettingsResponse(
    decimal? Price,
    IReadOnlyList<PublicLawyerAvailabilityResponse> Availability);

public sealed record PublicOnsiteConsultationSettingsResponse(
    IReadOnlyList<PublicLawyerAvailabilityResponse> Availability);

public sealed record PublicLawyerConsultationSettingsResponse(
    PublicOnlineConsultationSettingsResponse Online,
    PublicOnsiteConsultationSettingsResponse Onsite);

internal sealed class GetPublicLawyerConsultationSettingsQueryHandler(
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> repository,
    ILawyerDocumentPolicy documentPolicy)
    : IQueryHandler<GetPublicLawyerConsultationSettingsQuery, PublicLawyerConsultationSettingsResponse>
{
    public async Task<Result<PublicLawyerConsultationSettingsResponse>> Handle(
        GetPublicLawyerConsultationSettingsQuery query,
        CancellationToken cancellationToken)
    {
        var settings = await repository.FirstOrDefaultAsync(
            new PublicLawyerConsultationSettingsSpecification(query.LawyerId, documentPolicy),
            cancellationToken);
        return settings is null
            ? Result<PublicLawyerConsultationSettingsResponse>.Fail(LawyerErrors.NotEligibleForPublicDisplay)
            : Result<PublicLawyerConsultationSettingsResponse>.Ok(settings.ToResponse());
    }
}

internal sealed record PublicConsultationAvailabilitySnapshot(
    ConsultationType ConsultationType,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

internal sealed record PublicLawyerConsultationSettingsSnapshot(
    decimal? OnlinePrice,
    IReadOnlyList<PublicConsultationAvailabilitySnapshot> Availability)
{
    public PublicLawyerConsultationSettingsResponse ToResponse()
        => new(
            new PublicOnlineConsultationSettingsResponse(
                OnlinePrice,
                MapAvailability(ConsultationType.Online)),
            new PublicOnsiteConsultationSettingsResponse(
                MapAvailability(ConsultationType.Onsite)));

    private PublicLawyerAvailabilityResponse[] MapAvailability(ConsultationType consultationType)
        => Availability
            .Where(item => item.ConsultationType == consultationType)
            .OrderBy(item => item.DayOfWeek)
            .Select(item => new PublicLawyerAvailabilityResponse(
                item.DayOfWeek.ToString(),
                item.StartTime,
                item.EndTime))
            .ToArray();
}

internal sealed class PublicLawyerConsultationSettingsSpecification
    : PublicLawyerSpecification<PublicLawyerConsultationSettingsSnapshot>
{
    public PublicLawyerConsultationSettingsSpecification(
        Guid lawyerId,
        ILawyerDocumentPolicy documentPolicy)
    {
        AddCriteria(profile => profile.Id == lawyerId);
        ApplyPublicEligibility(documentPolicy);
        UseNoTracking();
        Select(profile => new PublicLawyerConsultationSettingsSnapshot(
            profile.ConsultationSettings == null
                ? null
                : profile.ConsultationSettings.OnlineConsultationPrice,
            profile.ConsultationSettings == null
                ? Array.Empty<PublicConsultationAvailabilitySnapshot>()
                : profile.ConsultationSettings.Availability
                    .Select(item => new PublicConsultationAvailabilitySnapshot(
                        item.ConsultationType,
                        item.DayOfWeek,
                        item.StartTime,
                        item.EndTime))
                    .ToArray()));
    }
}
