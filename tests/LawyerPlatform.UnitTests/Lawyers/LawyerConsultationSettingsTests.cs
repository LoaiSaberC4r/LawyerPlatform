using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.UnitTests.Lawyers;

public sealed class LawyerConsultationSettingsTests
{
    private static readonly Guid LawyerId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly DateTime NowUtc = new(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ValidOnlinePriceAndIndependentSchedulesAreAccepted()
    {
        var configured = LawyerConsultationSettings.Create(
            LawyerId,
            500m,
            [
                Period(ConsultationType.Online, DayOfWeek.Sunday, 10, 17),
                Period(ConsultationType.Onsite, DayOfWeek.Sunday, 9, 14)
            ],
            NowUtc);

        Assert.True(configured.IsSuccess);
        Assert.Equal(500m, configured.Value.OnlineConsultationPrice);
        Assert.Equal(2, configured.Value.Availability.Count);
        Assert.Contains(configured.Value.Availability, item =>
            item.ConsultationType == ConsultationType.Online && item.StartTime == new TimeOnly(10, 0));
        Assert.Contains(configured.Value.Availability, item =>
            item.ConsultationType == ConsultationType.Onsite && item.StartTime == new TimeOnly(9, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.001)]
    [InlineData(10000000000000000)]
    public void InvalidOnlinePricesAreRejected(decimal price)
    {
        var result = LawyerConsultationSettings.Create(LawyerId, price, [], NowUtc);

        Assert.Contains(result.Errors, error => error.Code == "Lawyer.ConsultationPriceInvalid");
    }

    [Theory]
    [InlineData(10, 0, 10, 0)]
    [InlineData(17, 0, 10, 0)]
    public void InvalidTimeRangesAreRejected(int startHour, int startMinute, int endHour, int endMinute)
    {
        var result = LawyerConsultationSettings.Create(
            LawyerId,
            500m,
            [new LawyerAvailabilityPeriod(
                ConsultationType.Online,
                DayOfWeek.Sunday,
                new TimeOnly(startHour, startMinute),
                new TimeOnly(endHour, endMinute))],
            NowUtc);

        Assert.Contains(result.Errors, error => error.Code == "Lawyer.AvailabilityInvalid");
    }

    [Theory]
    [InlineData(ConsultationType.Online)]
    [InlineData(ConsultationType.Onsite)]
    public void DuplicateDayWithinTypeIsRejected(ConsultationType consultationType)
    {
        var duplicate = LawyerConsultationSettings.Create(
            LawyerId,
            500m,
            [
                Period(consultationType, DayOfWeek.Sunday, 10, 17),
                Period(consultationType, DayOfWeek.Sunday, 12, 16)
            ],
            NowUtc);

        Assert.Contains(duplicate.Errors, error => error.Code == "Lawyer.DuplicateAvailabilityDay");
    }

    [Theory]
    [InlineData(ConsultationType.Online)]
    [InlineData(ConsultationType.Onsite)]
    public void MoreThanSevenPeriodsPerTypeIsRejected(ConsultationType consultationType)
    {
        var periods = Enum.GetValues<DayOfWeek>()
            .Select(day => Period(consultationType, day, 10, 17))
            .Append(Period(consultationType, DayOfWeek.Sunday, 18, 20))
            .ToArray();

        var result = LawyerConsultationSettings.Create(LawyerId, 500m, periods, NowUtc);

        Assert.Contains(result.Errors, error => error.Code == "Lawyer.AvailabilityInvalid");
    }

    [Fact]
    public void UpdateReconcilesByTypeAndDayAndPreservesMatchingChildIds()
    {
        var settings = LawyerConsultationSettings.Create(
            LawyerId,
            500m,
            [
                Period(ConsultationType.Online, DayOfWeek.Sunday, 10, 17),
                Period(ConsultationType.Online, DayOfWeek.Wednesday, 10, 17),
                Period(ConsultationType.Onsite, DayOfWeek.Monday, 9, 15)
            ],
            NowUtc).Value;
        var onlineSundayId = settings.Availability.Single(item =>
            item.ConsultationType == ConsultationType.Online &&
            item.DayOfWeek == DayOfWeek.Sunday).Id;
        var onsiteMondayId = settings.Availability.Single(item =>
            item.ConsultationType == ConsultationType.Onsite &&
            item.DayOfWeek == DayOfWeek.Monday).Id;
        var lawyer = CreateApprovedLawyer();
        var approvalStatus = lawyer.ApprovalStatus;

        var result = settings.Update(
            700m,
            [
                Period(ConsultationType.Online, DayOfWeek.Sunday, 8, 18),
                Period(ConsultationType.Online, DayOfWeek.Friday, 10, 16),
                Period(ConsultationType.Onsite, DayOfWeek.Monday, 8, 16),
                Period(ConsultationType.Onsite, DayOfWeek.Thursday, 9, 14)
            ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(700m, settings.OnlineConsultationPrice);
        Assert.Equal(onlineSundayId, settings.Availability.Single(item =>
            item.ConsultationType == ConsultationType.Online &&
            item.DayOfWeek == DayOfWeek.Sunday).Id);
        Assert.Equal(onsiteMondayId, settings.Availability.Single(item =>
            item.ConsultationType == ConsultationType.Onsite &&
            item.DayOfWeek == DayOfWeek.Monday).Id);
        Assert.DoesNotContain(settings.Availability, item =>
            item.ConsultationType == ConsultationType.Online &&
            item.DayOfWeek == DayOfWeek.Wednesday);
        Assert.Equal(approvalStatus, lawyer.ApprovalStatus);
    }

    private static LawyerAvailabilityPeriod Period(
        ConsultationType consultationType,
        DayOfWeek day,
        int startHour,
        int endHour)
        => new(consultationType, day, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));

    private static LawyerProfile CreateApprovedLawyer()
    {
        var account = UserAccount.CreateLawyer(
            "settings.lawyer",
            "SETTINGS.LAWYER",
            "settings.lawyer@example.test",
            "SETTINGS.LAWYER@EXAMPLE.TEST",
            "01012345678",
            "hash",
            NowUtc).Value;
        var lawyer = LawyerProfile.Create(account, "Settings Lawyer").Value;
        lawyer.SubmitForApproval(account.Id, true, true, NowUtc.AddMinutes(1));
        lawyer.Approve(Guid.NewGuid(), true, NowUtc.AddMinutes(2));
        return lawyer;
    }
}
