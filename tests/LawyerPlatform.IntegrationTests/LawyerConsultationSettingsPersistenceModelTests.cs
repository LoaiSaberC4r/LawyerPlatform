using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Persistence.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace LawyerPlatform.IntegrationTests;

public sealed class LawyerConsultationSettingsPersistenceModelTests
{
    [Fact]
    public void ConsultationTypeMigrationMapsAllExistingRowsToOnlineWithoutChangingPrices()
    {
        var migration = new AddOnlineAndOnsiteConsultationTypes();
        var addedTypeColumns = migration.UpOperations
            .OfType<AddColumnOperation>()
            .Where(operation => operation.Name == "ConsultationType")
            .ToArray();

        Assert.Equal(2, addedTypeColumns.Length);
        Assert.All(addedTypeColumns, operation =>
        {
            Assert.False(operation.IsNullable);
            Assert.Equal((int)ConsultationType.Online, operation.DefaultValue);
        });
        Assert.DoesNotContain(migration.UpOperations, operation =>
            operation is DropColumnOperation drop && drop.Name == "ConsultationPrice");
        Assert.DoesNotContain(migration.UpOperations, operation =>
            operation is AlterColumnOperation alter && alter.Name == "ConsultationPrice");
    }

    [Fact]
    public void SqlServerModelContainsSettingsAvailabilityAndNullablePriceSnapshotMappings()
    {
        var options = new DbContextOptionsBuilder<LawyerPlatformDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=LawyerPlatformModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new LawyerPlatformDbContext(options);
        var settings = context.Model.FindEntityType(typeof(LawyerConsultationSettings))!;
        var availability = context.Model.FindEntityType(typeof(LawyerAvailability))!;
        var request = context.Model.FindEntityType(typeof(ConsultationRequest))!;

        Assert.Equal("LawyerConsultationSettings", settings.GetTableName());
        Assert.Equal("LawyerAvailabilities", availability.GetTableName());
        Assert.Equal("decimal(18,2)", settings.FindProperty(nameof(LawyerConsultationSettings.OnlineConsultationPrice))!.GetColumnType());
        Assert.Equal("ConsultationPrice", settings.FindProperty(nameof(LawyerConsultationSettings.OnlineConsultationPrice))!
            .GetColumnName(StoreObjectIdentifier.Table("LawyerConsultationSettings", null)));
        Assert.True(settings.FindProperty(nameof(LawyerConsultationSettings.RowVersion))!.IsConcurrencyToken);
        Assert.Equal("time", availability.FindProperty(nameof(LawyerAvailability.StartTime))!.GetColumnType());
        Assert.Equal("time", availability.FindProperty(nameof(LawyerAvailability.EndTime))!.GetColumnType());
        Assert.Equal(typeof(int), availability.FindProperty(nameof(LawyerAvailability.ConsultationType))!
            .GetTypeMapping().Converter!.ProviderClrType);
        Assert.Equal(typeof(int), request.FindProperty(nameof(ConsultationRequest.ConsultationType))!
            .GetTypeMapping().Converter!.ProviderClrType);
        Assert.Equal("decimal(18,2)", request.FindProperty(nameof(ConsultationRequest.ConsultationPrice))!.GetColumnType());
        Assert.True(request.FindProperty(nameof(ConsultationRequest.ConsultationPrice))!.IsNullable);
        Assert.Contains(settings.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UX_LawyerConsultationSettings_LawyerProfileId");
        Assert.Contains(availability.GetIndexes(), index =>
            index.IsUnique &&
            index.GetDatabaseName() == "UX_LawyerAvailabilities_SettingsId_Type_DayOfWeek" &&
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(LawyerAvailability.LawyerConsultationSettingsId),
                nameof(LawyerAvailability.ConsultationType),
                nameof(LawyerAvailability.DayOfWeek)
            ]));
        Assert.Equal(DeleteBehavior.Restrict, settings.GetForeignKeys().Single().DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, availability.GetForeignKeys().Single().DeleteBehavior);
        Assert.Equal(ValueGenerated.Never, availability.FindProperty(nameof(LawyerAvailability.Id))!.ValueGenerated);
    }

    [Fact]
    public async Task ClientGeneratedAvailabilityIdsPreserveAggregateReplacementEntityStates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<LawyerPlatformDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new LawyerPlatformDbContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var nowUtc = new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
        var account = UserAccount.CreateLawyer(
            "settings.states",
            "SETTINGS.STATES",
            "settings.states@example.test",
            "SETTINGS.STATES@EXAMPLE.TEST",
            "01078888888",
            "integration-test-hash",
            nowUtc).Value;
        var profile = LawyerProfile.Create(account, "Settings State Lawyer").Value;
        var settings = LawyerConsultationSettings.Create(
            profile.Id,
            500m,
            [
                Period(DayOfWeek.Sunday, 10, 17),
                Period(DayOfWeek.Wednesday, 10, 20),
                Period(DayOfWeek.Thursday, 10, 19),
                Period(ConsultationType.Onsite, DayOfWeek.Sunday, 9, 14),
                Period(ConsultationType.Onsite, DayOfWeek.Monday, 9, 15)
            ],
            nowUtc).Value;
        context.LawyerProfiles.Add(profile);
        context.LawyerConsultationSettings.Add(settings);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var originalRowVersion = settings.RowVersion.ToArray();

        context.ChangeTracker.Clear();
        var tracked = await context.LawyerConsultationSettings
            .Include(item => item.Availability)
            .SingleAsync(item => item.Id == settings.Id, TestContext.Current.CancellationToken);
        var tokenManager = new ConcurrencyTokenManager(context);
        tokenManager.SetOriginalRowVersion(tracked, originalRowVersion);

        Assert.True(tracked.Update(
            500m,
            [
                Period(DayOfWeek.Sunday, 9, 18),
                Period(DayOfWeek.Wednesday, 10, 20),
                Period(DayOfWeek.Friday, 10, 16),
                Period(ConsultationType.Onsite, DayOfWeek.Sunday, 8, 15),
                Period(ConsultationType.Onsite, DayOfWeek.Thursday, 9, 14)
            ]).IsSuccess);
        tokenManager.MarkPropertyModified(tracked, item => item.OnlineConsultationPrice);
        context.ChangeTracker.DetectChanges();

        var settingsEntry = context.Entry(tracked);
        var rowVersionEntry = settingsEntry.Property(item => item.RowVersion);
        Assert.Equal(EntityState.Modified, settingsEntry.State);
        Assert.Equal(originalRowVersion, rowVersionEntry.OriginalValue);
        Assert.Equal(originalRowVersion, rowVersionEntry.CurrentValue);
        Assert.True(settingsEntry.Property(item => item.OnlineConsultationPrice).IsModified);

        var availabilityStates = context.ChangeTracker.Entries<LawyerAvailability>()
            .ToDictionary(
                entry => (entry.Entity.ConsultationType, entry.Entity.DayOfWeek),
                entry => entry.State);
        Assert.Equal(EntityState.Modified, availabilityStates[(ConsultationType.Online, DayOfWeek.Sunday)]);
        Assert.Equal(EntityState.Unchanged, availabilityStates[(ConsultationType.Online, DayOfWeek.Wednesday)]);
        Assert.Equal(EntityState.Deleted, availabilityStates[(ConsultationType.Online, DayOfWeek.Thursday)]);
        Assert.Equal(EntityState.Added, availabilityStates[(ConsultationType.Online, DayOfWeek.Friday)]);
        Assert.Equal(EntityState.Modified, availabilityStates[(ConsultationType.Onsite, DayOfWeek.Sunday)]);
        Assert.Equal(EntityState.Deleted, availabilityStates[(ConsultationType.Onsite, DayOfWeek.Monday)]);
        Assert.Equal(EntityState.Added, availabilityStates[(ConsultationType.Onsite, DayOfWeek.Thursday)]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.False(originalRowVersion.SequenceEqual(tracked.RowVersion));
    }

    private static LawyerAvailabilityPeriod Period(DayOfWeek day, int startHour, int endHour)
        => Period(ConsultationType.Online, day, startHour, endHour);

    private static LawyerAvailabilityPeriod Period(
        ConsultationType consultationType,
        DayOfWeek day,
        int startHour,
        int endHour)
        => new(consultationType, day, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));
}
