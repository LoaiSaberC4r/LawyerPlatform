using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LawyerPlatform.IntegrationTests;

public sealed class ConsultationRequestPersistenceModelTests
{
    [Fact]
    public void ModelContainsRequiredTablesRelationshipsIndexesAndRowVersion()
    {
        var options = new DbContextOptionsBuilder<LawyerPlatformDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new LawyerPlatformDbContext(options);
        var request = context.Model.FindEntityType(typeof(ConsultationRequest))!;
        var history = context.Model.FindEntityType(typeof(ConsultationRequestStatusHistory))!;

        Assert.Equal("ConsultationRequests", request.GetTableName());
        Assert.Equal("ConsultationRequestStatusHistory", history.GetTableName());
        Assert.True(request.FindProperty(nameof(ConsultationRequest.RowVersion))!.IsConcurrencyToken);
        Assert.Contains(request.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UX_ConsultationRequests_ReferenceNumber");
        Assert.Contains(request.GetIndexes(), index =>
            index.GetDatabaseName() == "IX_ConsultationRequests_LawyerProfileId_Status_CreatedOnUtc");
        Assert.Contains(request.GetIndexes(), index =>
            index.GetDatabaseName() == "IX_ConsultationRequests_ClientProfileId_CreatedOnUtc");
        Assert.Contains(history.GetIndexes(), index =>
            index.GetDatabaseName() == "IX_ConsultationRequestStatusHistory_ConsultationRequestId_ChangedOnUtc");
        Assert.All(request.GetForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.All(history.GetForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }
}
