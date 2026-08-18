using System.Data.Common;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LawyerPlatform.IntegrationTests;

[Collection(LawyerPlatformSqlServerTestGroup.Name)]
public sealed class LawyerConsultationSettingsSqlServerTests(LawyerPlatformSqlServerFixture fixture)
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SqlServerAllowsSequentialSettingsUpdatesUsingLatestRowVersionAndRejectsStaleToken()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var nowUtc = DateTime.UtcNow;
        var account = UserAccount.CreateLawyer(
            $"sequential-{suffix[..8]}",
            $"SEQUENTIAL-{suffix[..8]}",
            $"sequential-{suffix}@example.test",
            $"SEQUENTIAL-{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
            $"010{Random.Shared.Next(10_000_000, 99_999_999)}",
            "integration-test-hash",
            nowUtc).Value;
        var profile = LawyerProfile.Create(account, "SQL Sequential Settings Lawyer").Value;
        var settings = LawyerConsultationSettings.Create(
            profile.Id,
            500m,
            [Period(DayOfWeek.Sunday, 10, 17)],
            nowUtc).Value;

        await using (var seedContext = fixture.CreateContext())
        {
            seedContext.LawyerProfiles.Add(profile);
            seedContext.LawyerConsultationSettings.Add(settings);
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var rowVersions = new List<byte[]> { settings.RowVersion.ToArray() };
        var updates = new[]
        {
            new SettingsUpdate(500m,
            [
                Period(DayOfWeek.Sunday, 10, 17),
                Period(DayOfWeek.Wednesday, 10, 20)
            ]),
            new SettingsUpdate(600m,
            [
                Period(DayOfWeek.Sunday, 10, 17),
                Period(DayOfWeek.Wednesday, 10, 20)
            ]),
            new SettingsUpdate(600m,
            [
                Period(DayOfWeek.Sunday, 9, 18),
                Period(DayOfWeek.Wednesday, 10, 20),
                Period(DayOfWeek.Thursday, 10, 19)
            ]),
            new SettingsUpdate(600m,
            [
                Period(DayOfWeek.Sunday, 9, 18),
                Period(DayOfWeek.Wednesday, 11, 20),
                Period(DayOfWeek.Friday, 10, 16)
            ]),
            new SettingsUpdate(650m, [])
        };
        var sqlCapture = new SqlCaptureInterceptor();

        for (var index = 0; index < updates.Length; index++)
        {
            await using var updateContext = CreateSqlCaptureContext(sqlCapture);
            var tracked = await updateContext.LawyerConsultationSettings
                .Include(item => item.Availability)
                .SingleAsync(item => item.Id == settings.Id, TestContext.Current.CancellationToken);
            var tokenManager = new ConcurrencyTokenManager(updateContext);
            tokenManager.SetOriginalRowVersion(tracked, rowVersions[^1]);
            Assert.True(tracked.Update(updates[index].Price, updates[index].Availability).IsSuccess);
            tokenManager.MarkPropertyModified(tracked, item => item.OnlineConsultationPrice);
            updateContext.ChangeTracker.DetectChanges();

            Assert.Equal(rowVersions[^1], updateContext.Entry(tracked).Property(item => item.RowVersion).OriginalValue);
            Assert.True(updateContext.Entry(tracked).Property(item => item.OnlineConsultationPrice).IsModified);
            if (index == 0)
            {
                Assert.Equal(
                    EntityState.Added,
                    updateContext.ChangeTracker.Entries<LawyerAvailability>()
                        .Single(entry => entry.Entity.DayOfWeek == DayOfWeek.Wednesday).State);
            }

            await updateContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            Assert.NotEmpty(tracked.RowVersion);
            Assert.False(rowVersions[^1].SequenceEqual(tracked.RowVersion));
            rowVersions.Add(tracked.RowVersion.ToArray());
        }

        Assert.Contains(sqlCapture.Commands, command =>
            command.Contains("UPDATE [LawyerConsultationSettings]", StringComparison.Ordinal) &&
            command.Contains("WHERE [Id] =", StringComparison.Ordinal) &&
            command.Contains("[RowVersion] =", StringComparison.Ordinal));

        await using var staleContext = fixture.CreateContext();
        var staleCopy = await staleContext.LawyerConsultationSettings
            .Include(item => item.Availability)
            .SingleAsync(item => item.Id == settings.Id, TestContext.Current.CancellationToken);
        var staleTokenManager = new ConcurrencyTokenManager(staleContext);
        staleTokenManager.SetOriginalRowVersion(staleCopy, rowVersions[^3]);
        Assert.True(staleCopy.Update(999m, []).IsSuccess);
        staleTokenManager.MarkPropertyModified(staleCopy, item => item.OnlineConsultationPrice);

        var exception = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            staleContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Single(exception.Entries);
        Assert.IsType<LawyerConsultationSettings>(exception.Entries[0].Entity);
        var mapper = new LawyerPlatformUniqueConstraintExceptionMapper();
        Assert.True(mapper.TryMap(exception, out var error));
        Assert.Equal("Lawyer.ConsultationSettingsConcurrencyConflict", error.Code);

        await using var verificationContext = fixture.CreateContext();
        Assert.Equal(
            650m,
            (await verificationContext.LawyerConsultationSettings.AsNoTracking()
                .SingleAsync(item => item.Id == settings.Id, TestContext.Current.CancellationToken))
            .OnlineConsultationPrice);
    }

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
            [
                new LawyerAvailabilityPeriod(ConsultationType.Online, DayOfWeek.Sunday, new TimeOnly(10, 15), new TimeOnly(17, 45)),
                new LawyerAvailabilityPeriod(ConsultationType.Onsite, DayOfWeek.Sunday, new TimeOnly(9, 0), new TimeOnly(14, 0))
            ],
            nowUtc).Value;
        var historicalRequest = ConsultationRequest.CreateForGuest(
            $"CR-{suffix[..20]}",
            "Historical Guest",
            "01012345678",
            null,
            profile.Id,
            ConsultationType.Onsite,
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
            Assert.Equal(500.25m, stored.OnlineConsultationPrice);
            var online = stored.Availability.Single(item => item.ConsultationType == ConsultationType.Online);
            var onsite = stored.Availability.Single(item => item.ConsultationType == ConsultationType.Onsite);
            Assert.Equal(new TimeOnly(10, 15), online.StartTime);
            Assert.Equal(new TimeOnly(17, 45), online.EndTime);
            Assert.Equal(new TimeOnly(9, 0), onsite.StartTime);
            Assert.Equal(new TimeOnly(14, 0), onsite.EndTime);
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
                    $"INSERT INTO LawyerAvailabilities (Id, LawyerConsultationSettingsId, ConsultationType, DayOfWeek, StartTime, EndTime) VALUES ({Guid.NewGuid()}, {settings.Id}, {(int)ConsultationType.Online}, {(int)DayOfWeek.Sunday}, {new TimeOnly(12, 0)}, {new TimeOnly(16, 0)})",
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
            new LawyerAvailabilityPeriod(ConsultationType.Online, DayOfWeek.Sunday, new TimeOnly(10, 15), new TimeOnly(17, 45))]).IsSuccess);
        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.True(secondCopy.Update(800m, [
            new LawyerAvailabilityPeriod(ConsultationType.Online, DayOfWeek.Sunday, new TimeOnly(10, 15), new TimeOnly(17, 45))]).IsSuccess);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private LawyerPlatformDbContext CreateSqlCaptureContext(SqlCaptureInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<LawyerPlatformDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        return new LawyerPlatformDbContext(options);
    }

    private static LawyerAvailabilityPeriod Period(DayOfWeek day, int startHour, int endHour)
        => new(ConsultationType.Online, day, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));

    private sealed record SettingsUpdate(
        decimal Price,
        IReadOnlyCollection<LawyerAvailabilityPeriod> Availability);

    private sealed class SqlCaptureInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
