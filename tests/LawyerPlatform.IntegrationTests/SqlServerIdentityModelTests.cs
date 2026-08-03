using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LawyerPlatform.IntegrationTests;

public sealed class SqlServerIdentityModelTests
{
    [Fact]
    public void IdentityModel_ContainsRequiredTablesIndexesAndRestrictedRelationships()
    {
        using var context = CreateContext();
        var model = context.Model;
        var requiredTables = new[]
        {
            "UserAccounts", "ClientProfiles", "LawyerProfiles", "Governorates",
            "Cities", "Areas", "LegalSpecializations", "LawyerOffices",
            "LawyerSpecializations", "LawyerDocuments", "LawyerApprovalStatusHistory"
        };

        foreach (var table in requiredTables)
        {
            Assert.Contains(model.GetEntityTypes(), entity => entity.GetTableName() == table);
        }

        var indexNames = model.GetEntityTypes()
            .SelectMany(entity => entity.GetIndexes())
            .Select(index => index.GetDatabaseName())
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("UX_UserAccounts_NormalizedUserName", indexNames);
        Assert.Contains("UX_UserAccounts_NormalizedEmail", indexNames);
        Assert.Contains("UX_UserAccounts_PhoneNumber", indexNames);
        Assert.Contains("UX_ClientProfiles_UserAccountId", indexNames);
        Assert.Contains("UX_LawyerProfiles_UserAccountId", indexNames);
        Assert.Contains("UX_LawyerProfiles_ProfessionalRegistrationNumber", indexNames);
        Assert.Contains("UX_LawyerOffices_LawyerProfileId_Primary", indexNames);
        Assert.Contains("IX_LawyerOffices_GovernorateId_CityId_AreaId", indexNames);
        Assert.Contains("IX_LawyerSpecializations_LegalSpecializationId_LawyerProfileId", indexNames);
        Assert.Contains("IX_LawyerDocuments_LawyerProfileId_DocumentType_IsDeleted", indexNames);
        Assert.Contains("IX_LawyerApprovalStatusHistory_LawyerProfileId_ChangedOnUtc", indexNames);
        Assert.Contains("IX_LawyerProfiles_ApprovalStatus_SubmittedOnUtc", indexNames);

        var identityForeignKeys = model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is "ClientProfiles" or "LawyerProfiles" or "Cities" or "Areas" or
                "LawyerOffices" or "LawyerSpecializations" or "LawyerDocuments" or "LawyerApprovalStatusHistory")
            .SelectMany(entity => entity.GetForeignKeys())
            .ToArray();
        Assert.All(identityForeignKeys, foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void IdentityModel_ConfiguresAllRowVersionsAsGeneratedConcurrencyTokens()
    {
        using var context = CreateContext();
        var rowVersions = context.Model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null)
            .Select(entity => entity.FindProperty("RowVersion"))
            .Where(property => property is not null)
            .Cast<IProperty>()
            .ToArray();

        Assert.True(rowVersions.Length >= 10);
        Assert.All(rowVersions, property =>
        {
            Assert.True(property.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
        });
    }

    private static LawyerPlatformDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LawyerPlatformDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=LawyerPlatformModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new LawyerPlatformDbContext(options);
    }
}
