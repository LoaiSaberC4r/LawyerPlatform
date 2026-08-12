using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.ConsultationSettings.Common;

public sealed record LawyerAvailabilityResponse(
    string DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed record LawyerConsultationSettingsResponse(
    decimal? ConsultationPrice,
    IReadOnlyList<LawyerAvailabilityResponse> Availability,
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
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

internal sealed record LawyerConsultationSettingsSnapshot(
    decimal ConsultationPrice,
    IReadOnlyList<LawyerAvailabilitySnapshot> Availability,
    byte[] RowVersion)
{
    public LawyerConsultationSettingsResponse ToResponse()
        => new(
            ConsultationPrice,
            Availability
                .OrderBy(item => item.DayOfWeek)
                .Select(item => new LawyerAvailabilityResponse(
                    item.DayOfWeek.ToString(),
                    item.StartTime,
                    item.EndTime))
                .ToArray(),
            RowVersionCodec.Encode(RowVersion));
}

internal sealed class LawyerConsultationSettingsByLawyerIdSpecification
    : Specification<LawyerConsultationSettings, LawyerConsultationSettingsSnapshot>
{
    public LawyerConsultationSettingsByLawyerIdSpecification(Guid lawyerProfileId)
    {
        AddCriteria(settings => settings.LawyerProfileId == lawyerProfileId);
        UseNoTracking();
        Select(settings => new LawyerConsultationSettingsSnapshot(
            settings.ConsultationPrice,
            settings.Availability
                .OrderBy(availability => availability.DayOfWeek)
                .Select(availability => new LawyerAvailabilitySnapshot(
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
            settings.ConsultationPrice,
            settings.Availability
                .OrderBy(item => item.DayOfWeek)
                .Select(item => new LawyerAvailabilityResponse(
                    item.DayOfWeek.ToString(),
                    item.StartTime,
                    item.EndTime))
                .ToArray(),
            RowVersionCodec.Encode(settings.RowVersion));
}
