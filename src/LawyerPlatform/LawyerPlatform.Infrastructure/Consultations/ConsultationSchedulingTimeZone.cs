using LawyerPlatform.Application.Abstractions.Consultations;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Consultations;

internal sealed class ConsultationSchedulingTimeZone : IConsultationSchedulingTimeZone
{
    private readonly TimeZoneInfo _businessTimeZone;

    public ConsultationSchedulingTimeZone(IOptions<ConsultationSchedulingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _businessTimeZone = Resolve(options.Value.TimeZoneId);
    }

    public DateTime ConvertUtcToBusinessLocal(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The consultation appointment must be UTC.", nameof(utcDateTime));
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _businessTimeZone);
    }

    internal static bool CanResolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            _ = Resolve(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static TimeZoneInfo Resolve(string timeZoneId)
    {
        var normalized = timeZoneId.Trim();
        if (TryFind(normalized, out var timeZone))
        {
            return timeZone;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(normalized, out var windowsId) &&
            TryFind(windowsId, out timeZone))
        {
            return timeZone;
        }

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(normalized, out var ianaId) &&
            TryFind(ianaId, out timeZone))
        {
            return timeZone;
        }

        throw new TimeZoneNotFoundException($"The consultation scheduling time zone '{normalized}' could not be resolved.");
    }

    private static bool TryFind(string timeZoneId, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = null!;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = null!;
            return false;
        }
    }
}
