using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LawyerPlatform.UnitTests.Persistence;

public sealed class UniqueConstraintExceptionMapperTests
{
    [Fact]
    public void CurrentLawyerAvailabilityIndexMapsToStableDuplicateDayError()
    {
        var exception = ProviderException(
            "UX_LawyerAvailabilities_SettingsId_Type_DayOfWeek");

        var mapped = new LawyerPlatformUniqueConstraintExceptionMapper()
            .TryMap(exception, out var error);

        Assert.True(mapped);
        Assert.Equal("Lawyer.DuplicateAvailabilityDay", error.Code);
    }

    [Fact]
    public void ConsultationReferenceIndexMapsToStableReferenceConflictError()
    {
        var exception = ProviderException("UX_ConsultationRequests_ReferenceNumber");

        var mapped = new LawyerPlatformUniqueConstraintExceptionMapper()
            .TryMap(exception, out var error);

        Assert.True(mapped);
        Assert.Equal("ConsultationRequest.ReferenceNumberConflict", error.Code);
    }

    private static DbUpdateException ProviderException(string databaseObjectName)
        => new(
            "A persistence operation failed.",
            new InvalidOperationException($"Unique index '{databaseObjectName}' was violated."));
}
