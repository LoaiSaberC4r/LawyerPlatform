using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.UnitTests.Lawyers;

public sealed class LawyerConsultationSettingsTests
{
    private static readonly Guid LawyerId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly DateTime NowUtc = new(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PositivePriceValidPeriodAndEmptyAvailabilityAreAccepted()
    {
        var configured = LawyerConsultationSettings.Create(
            LawyerId,
            500m,
            [new LawyerAvailabilityPeriod(DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(17, 0))],
            NowUtc);
        var empty = LawyerConsultationSettings.Create(LawyerId, 500m, [], NowUtc);

        Assert.True(configured.IsSuccess);
        Assert.True(empty.IsSuccess);
        Assert.Empty(empty.Value.Availability);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.001)]
    public void InvalidPricesAreRejected(decimal price)
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
                DayOfWeek.Sunday,
                new TimeOnly(startHour, startMinute),
                new TimeOnly(endHour, endMinute))],
            NowUtc);

        Assert.Contains(result.Errors, error => error.Code == "Lawyer.AvailabilityInvalid");
    }

    [Fact]
    public void DuplicateDayIsRejectedAndSevenUniqueDaysAreAccepted()
    {
        var duplicate = LawyerConsultationSettings.Create(
            LawyerId,
            500m,
            [
                new LawyerAvailabilityPeriod(DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(17, 0)),
                new LawyerAvailabilityPeriod(DayOfWeek.Sunday, new TimeOnly(12, 0), new TimeOnly(16, 0))
            ],
            NowUtc);
        var seven = LawyerConsultationSettings.Create(
            LawyerId,
            500m,
            Enum.GetValues<DayOfWeek>()
                .Select(day => new LawyerAvailabilityPeriod(day, new TimeOnly(10, 0), new TimeOnly(17, 0)))
                .ToArray(),
            NowUtc);

        Assert.Contains(duplicate.Errors, error => error.Code == "Lawyer.DuplicateAvailabilityDay");
        Assert.True(seven.IsSuccess);
        Assert.Equal(7, seven.Value.Availability.Count);
    }

    [Fact]
    public void UpdateFullyReplacesAvailabilityAndDoesNotChangeLawyerApprovalStatus()
    {
        var settings = LawyerConsultationSettings.Create(
            LawyerId,
            500m,
            [
                new LawyerAvailabilityPeriod(DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(17, 0)),
                new LawyerAvailabilityPeriod(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(17, 0)),
                new LawyerAvailabilityPeriod(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(17, 0))
            ],
            NowUtc).Value;
        var lawyer = CreateApprovedLawyer();
        var approvalStatus = lawyer.ApprovalStatus;

        var result = settings.Update(
            700m,
            [
                new LawyerAvailabilityPeriod(DayOfWeek.Sunday, new TimeOnly(9, 0), new TimeOnly(18, 0)),
                new LawyerAvailabilityPeriod(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(17, 0))
            ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(700m, settings.ConsultationPrice);
        Assert.Equal([DayOfWeek.Sunday, DayOfWeek.Monday], settings.Availability.Select(item => item.DayOfWeek).ToArray());
        Assert.Equal(approvalStatus, lawyer.ApprovalStatus);
    }

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
