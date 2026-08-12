using LawyerPlatform.Infrastructure.Consultations;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.UnitTests.Configuration;

public sealed class ConsultationSchedulingTimeZoneTests
{
    [Fact]
    public void UtcAppointmentIsConvertedBeforeDayAndTimeAreRead()
    {
        var schedulingTimeZone = new ConsultationSchedulingTimeZone(
            Options.Create(new ConsultationSchedulingOptions { TimeZoneId = "Africa/Cairo" }));
        var saturdayUtc = new DateTime(2026, 8, 15, 22, 30, 0, DateTimeKind.Utc);

        var local = schedulingTimeZone.ConvertUtcToBusinessLocal(saturdayUtc);

        Assert.Equal(DayOfWeek.Sunday, local.DayOfWeek);
        Assert.Equal(new TimeOnly(1, 30), TimeOnly.FromDateTime(local));
    }

    [Theory]
    [InlineData("Africa/Cairo")]
    [InlineData("Egypt Standard Time")]
    public void IanaAndWindowsIdentifiersCanBeResolved(string timeZoneId)
        => Assert.True(ConsultationSchedulingTimeZone.CanResolve(timeZoneId));
}
