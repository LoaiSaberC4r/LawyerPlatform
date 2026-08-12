using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LawyerPlatform.IntegrationTests;

[Collection(LawyerPlatformSqlServerTestGroup.Name)]
public sealed class LawyerConsultationSettingsSqlServerTests(LawyerPlatformSqlServerFixture fixture)
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SqlServerEnforcesSettingsMappingsUniquenessConcurrencyAndNullableSnapshots()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var nowUtc = DateTime.UtcNow;
        var account = UserAccount.CreateLawyer(
            $"settings-{suffix[..8]}",
            $"SETTINGS-{suffix[..8]}",
            $"settings-{suffix}@example.test",
            $"SETTINGS-{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
            $"010{Random.Shared.Next(10_000_000, 99_999_999)}",
            "integration-test-hash",
            nowUtc).Value;
        var profile = LawyerProfile.Create(account, "SQL Settings Lawyer").Value;
        var settings = LawyerConsultationSettings.Create(
            profile.Id,
            500.25m,
            [new LawyerAvailabilityPeriod(DayOfWeek.Sunday, new TimeOnly(10, 15), new TimeOnly(17, 45))],
            nowUtc).Value;
        var historicalRequest = ConsultationRequest.CreateForGuest(
            $"CR-{suffix[..20]}",
            "Historical Guest",
            "01012345678",
            null,
            profile.Id,
            null,
            "Historical nullable price request",
            null,
            nowUtc).Value;

        await using (var seedContext = fixture.CreateContext())
        {
            seedContext.LawyerProfiles.Add(profile);
            seedContext.LawyerConsultationSettings.Add(settings);
            seedContext.ConsultationRequests.Add(historicalRequest);
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var readContext = fixture.CreateContext())
        {
            var stored = await readContext.LawyerConsultationSettings.AsNoTracking()
                .Include(item => item.Availability)
                .SingleAsync(item => item.Id == settings.Id, TestContext.Current.CancellationToken);
            Assert.Equal(500.25m, stored.ConsultationPrice);
            Assert.Equal(new TimeOnly(10, 15), stored.Availability.Single().StartTime);
            Assert.Equal(new TimeOnly(17, 45), stored.Availability.Single().EndTime);
            Assert.NotEmpty(stored.RowVersion);
            Assert.Null((await readContext.ConsultationRequests.AsNoTracking().SingleAsync(
                item => item.Id == historicalRequest.Id,
                TestContext.Current.CancellationToken)).ConsultationPrice);
        }

        await using (var duplicateContext = fixture.CreateContext())
        {
            duplicateContext.LawyerConsultationSettings.Add(LawyerConsultationSettings.Create(
                profile.Id,
                600m,
                [],
                nowUtc).Value);
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                duplicateContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using (var duplicateAvailabilityContext = fixture.CreateContext())
        {
            await Assert.ThrowsAsync<SqlException>(() =>
                duplicateAvailabilityContext.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO LawyerAvailabilities (Id, LawyerConsultationSettingsId, DayOfWeek, StartTime, EndTime) VALUES ({Guid.NewGuid()}, {settings.Id}, {(int)DayOfWeek.Sunday}, {new TimeOnly(12, 0)}, {new TimeOnly(16, 0)})",
                    TestContext.Current.CancellationToken));
        }

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var firstCopy = await firstContext.LawyerConsultationSettings
            .Include(item => item.Availability)
            .SingleAsync(item => item.Id == settings.Id, TestContext.Current.CancellationToken);
        var secondCopy = await secondContext.LawyerConsultationSettings
            .Include(item => item.Availability)
            .SingleAsync(item => item.Id == settings.Id, TestContext.Current.CancellationToken);
        Assert.True(firstCopy.Update(700m, [
            new LawyerAvailabilityPeriod(DayOfWeek.Sunday, new TimeOnly(10, 15), new TimeOnly(17, 45))]).IsSuccess);
        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.True(secondCopy.Update(800m, [
            new LawyerAvailabilityPeriod(DayOfWeek.Sunday, new TimeOnly(10, 15), new TimeOnly(17, 45))]).IsSuccess);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
