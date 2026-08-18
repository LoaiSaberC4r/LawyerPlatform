using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.ConsultationSettings.Common;

public sealed record LawyerAvailabilityResponse(
    string DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed record LawyerOnlineConsultationSettingsResponse(
    decimal? Price,
    IReadOnlyList<LawyerAvailabilityResponse> Availability);

public sealed record LawyerOnsiteConsultationSettingsResponse(
    IReadOnlyList<LawyerAvailabilityResponse> Availability);

public sealed record LawyerConsultationSettingsResponse(
    LawyerOnlineConsultationSettingsResponse Online,
    LawyerOnsiteConsultationSettingsResponse Onsite,
    string? RowVersion);

internal sealed record CurrentLawyerIdSnapshot(Guid Id);

internal sealed class CurrentLawyerIdSpecification
    : Specification<LawyerProfile, CurrentLawyerIdSnapshot>
{
    public CurrentLawyerIdSpecification(Guid userAccountId)
    {
        AddCriteria(profile => profile.UserAccountId == userAccountId && !profile.IsDeleted);
        UseNoTracking();
        Select(profile => new CurrentLawyerIdSnapshot(profile.Id));
    }
}

internal sealed record LawyerAvailabilitySnapshot(
    ConsultationType ConsultationType,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

internal sealed record LawyerConsultationSettingsSnapshot(
    decimal OnlineConsultationPrice,
    IReadOnlyList<LawyerAvailabilitySnapshot> Availability,
    byte[] RowVersion)
{
    public LawyerConsultationSettingsResponse ToResponse()
        => new(
            new LawyerOnlineConsultationSettingsResponse(
                OnlineConsultationPrice,
                MapAvailability(ConsultationType.Online)),
            new LawyerOnsiteConsultationSettingsResponse(
                MapAvailability(ConsultationType.Onsite)),
            RowVersionCodec.Encode(RowVersion));

    private LawyerAvailabilityResponse[] MapAvailability(ConsultationType consultationType)
        => Availability
                .Where(item => item.ConsultationType == consultationType)
                .OrderBy(item => item.DayOfWeek)
                .Select(item => new LawyerAvailabilityResponse(
                    item.DayOfWeek.ToString(),
                    item.StartTime,
                    item.EndTime))
                .ToArray();
}

internal sealed class LawyerConsultationSettingsByLawyerIdSpecification
    : Specification<LawyerConsultationSettings, LawyerConsultationSettingsSnapshot>
{
    public LawyerConsultationSettingsByLawyerIdSpecification(Guid lawyerProfileId)
    {
        AddCriteria(settings => settings.LawyerProfileId == lawyerProfileId);
        UseNoTracking();
        Select(settings => new LawyerConsultationSettingsSnapshot(
            settings.OnlineConsultationPrice,
            settings.Availability
                .OrderBy(availability => availability.DayOfWeek)
                .Select(availability => new LawyerAvailabilitySnapshot(
                    availability.ConsultationType,
                    availability.DayOfWeek,
                    availability.StartTime,
                    availability.EndTime))
                .ToArray(),
            settings.RowVersion));
    }
}

internal sealed class LawyerConsultationSettingsForUpdateSpecification
    : Specification<LawyerConsultationSettings>
{
    public LawyerConsultationSettingsForUpdateSpecification(Guid lawyerProfileId)
    {
        AddCriteria(settings => settings.LawyerProfileId == lawyerProfileId);
        AddInclude(settings => settings.Availability);
        UseTracking();
    }
}

internal static class LawyerConsultationSettingsMapper
{
    public static LawyerConsultationSettingsResponse Map(LawyerConsultationSettings settings)
        => new(
            new LawyerOnlineConsultationSettingsResponse(
                settings.OnlineConsultationPrice,
                MapAvailability(settings, ConsultationType.Online)),
            new LawyerOnsiteConsultationSettingsResponse(
                MapAvailability(settings, ConsultationType.Onsite)),
            RowVersionCodec.Encode(settings.RowVersion));

    private static LawyerAvailabilityResponse[] MapAvailability(
        LawyerConsultationSettings settings,
        ConsultationType consultationType)
        => settings.Availability
                .Where(item => item.ConsultationType == consultationType)
                .OrderBy(item => item.DayOfWeek)
                .Select(item => new LawyerAvailabilityResponse(
                    item.DayOfWeek.ToString(),
                    item.StartTime,
                    item.EndTime))
                .ToArray();
}
