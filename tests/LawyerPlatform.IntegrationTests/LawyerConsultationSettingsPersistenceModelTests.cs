using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LawyerPlatform.IntegrationTests;

public sealed class LawyerConsultationSettingsPersistenceModelTests
{
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
        Assert.Equal("decimal(18,2)", settings.FindProperty(nameof(LawyerConsultationSettings.ConsultationPrice))!.GetColumnType());
        Assert.True(settings.FindProperty(nameof(LawyerConsultationSettings.RowVersion))!.IsConcurrencyToken);
        Assert.Equal("time", availability.FindProperty(nameof(LawyerAvailability.StartTime))!.GetColumnType());
        Assert.Equal("time", availability.FindProperty(nameof(LawyerAvailability.EndTime))!.GetColumnType());
        Assert.Equal("decimal(18,2)", request.FindProperty(nameof(ConsultationRequest.ConsultationPrice))!.GetColumnType());
        Assert.True(request.FindProperty(nameof(ConsultationRequest.ConsultationPrice))!.IsNullable);
        Assert.Contains(settings.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UX_LawyerConsultationSettings_LawyerProfileId");
        Assert.Contains(availability.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UX_LawyerAvailabilities_SettingsId_DayOfWeek");
        Assert.Equal(DeleteBehavior.Restrict, settings.GetForeignKeys().Single().DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, availability.GetForeignKeys().Single().DeleteBehavior);
    }
}
