using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace LawyerPlatform.IntegrationTests;

public sealed class LawyerPersistenceModelTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task LawyerOnboardingModel_HasExpectedTablesIndexesConcurrencyAndRestrictRelationships()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        var model = dbContext.Model;

        var office = model.FindEntityType(typeof(LawyerOffice))!;
        var document = model.FindEntityType(typeof(LawyerDocument))!;
        var specialization = model.FindEntityType(typeof(LawyerSpecialization))!;
        var history = model.FindEntityType(typeof(LawyerApprovalStatusHistory))!;
        var profile = model.FindEntityType(typeof(LawyerProfile))!;

        Assert.Equal("LawyerOffices", office.GetTableName());
        Assert.Equal("LawyerDocuments", document.GetTableName());
        Assert.Equal("LawyerSpecializations", specialization.GetTableName());
        Assert.Equal("LawyerApprovalStatusHistory", history.GetTableName());

        Assert.Equal(
            [nameof(LawyerSpecialization.LawyerProfileId), nameof(LawyerSpecialization.LegalSpecializationId)],
            specialization.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());
        Assert.Contains(office.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UX_LawyerOffices_LawyerProfileId_Primary");
        Assert.Contains(document.GetIndexes(), index =>
            index.GetDatabaseName() == "IX_LawyerDocuments_LawyerProfileId_DocumentType_IsDeleted");
        Assert.Contains(specialization.GetIndexes(), index =>
            index.GetDatabaseName() == "IX_LawyerSpecializations_LegalSpecializationId_LawyerProfileId");
        Assert.Contains(history.GetIndexes(), index =>
            index.GetDatabaseName() == "IX_LawyerApprovalStatusHistory_LawyerProfileId_ChangedOnUtc");
        Assert.Contains(profile.GetIndexes(), index =>
            index.GetDatabaseName() == "IX_LawyerProfiles_ApprovalStatus_SubmittedOnUtc");
        Assert.Contains(profile.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UX_LawyerProfiles_ProfessionalRegistrationNumber");

        Assert.True(profile.FindProperty(nameof(LawyerProfile.RowVersion))!.IsConcurrencyToken);
        Assert.True(office.FindProperty(nameof(LawyerOffice.RowVersion))!.IsConcurrencyToken);
        Assert.True(document.FindProperty(nameof(LawyerDocument.RowVersion))!.IsConcurrencyToken);
        var latitude = office.FindProperty(nameof(LawyerOffice.Latitude))!;
        var longitude = office.FindProperty(nameof(LawyerOffice.Longitude))!;
        Assert.True(latitude.IsNullable);
        Assert.Equal(9, latitude.GetPrecision());
        Assert.Equal(6, latitude.GetScale());
        Assert.True(longitude.IsNullable);
        Assert.Equal(9, longitude.GetPrecision());
        Assert.Equal(6, longitude.GetScale());
        Assert.All(
            new[] { office, document, specialization, history }.SelectMany(entity => entity.GetForeignKeys()),
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));

        await AssertTableExistsAsync(dbContext, "LawyerOffices");
        await AssertTableExistsAsync(dbContext, "LawyerDocuments");
        await AssertTableExistsAsync(dbContext, "LawyerSpecializations");
        await AssertTableExistsAsync(dbContext, "LawyerApprovalStatusHistory");
    }

    private static async Task AssertTableExistsAsync(LawyerPlatformDbContext dbContext, string tableName)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);
        var count = Convert.ToInt64(
            await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
            CultureInfo.InvariantCulture);
        Assert.Equal(1L, count);
    }
}
