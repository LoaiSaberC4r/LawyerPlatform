using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Domain.ReferenceData;
using LawyerPlatform.Infrastructure.Consultations;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace LawyerPlatform.IntegrationTests;

[Collection(LawyerPlatformSqlServerTestGroup.Name)]
public sealed class ReferenceDataConcurrencySqlServerTests(LawyerPlatformSqlServerFixture fixture)
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentReferenceDataDuplicatesAllowOneWinnerAndMapEveryLoserToStableConflict()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        int governorateParentId;
        int cityParentId;
        int[] governorateIds;
        int[] cityIds;
        int[] areaIds;
        int[] specializationIds;

        await using (var seedContext = fixture.CreateContext())
        {
            var generator = new SqlServerReferenceDataIdGenerator(seedContext);
            governorateIds =
            [
                await generator.NextGovernorateIdAsync(TestContext.Current.CancellationToken),
                await generator.NextGovernorateIdAsync(TestContext.Current.CancellationToken)
            ];
            cityIds =
            [
                await generator.NextCityIdAsync(TestContext.Current.CancellationToken),
                await generator.NextCityIdAsync(TestContext.Current.CancellationToken)
            ];
            areaIds =
            [
                await generator.NextAreaIdAsync(TestContext.Current.CancellationToken),
                await generator.NextAreaIdAsync(TestContext.Current.CancellationToken)
            ];
            specializationIds =
            [
                await generator.NextLegalSpecializationIdAsync(TestContext.Current.CancellationToken),
                await generator.NextLegalSpecializationIdAsync(TestContext.Current.CancellationToken)
            ];
            governorateParentId = await generator.NextGovernorateIdAsync(TestContext.Current.CancellationToken);
            cityParentId = await generator.NextCityIdAsync(TestContext.Current.CancellationToken);

            seedContext.Governorates.Add(Governorate.Create(
                governorateParentId,
                $"محافظة أصل {suffix}",
                $"Parent Governorate {suffix}",
                1).Value);
            seedContext.Cities.Add(City.Create(
                cityParentId,
                governorateParentId,
                $"مدينة أصل {suffix}",
                $"Parent City {suffix}",
                1).Value);
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await AssertOneStableConflictAsync(
            Governorate.Create(
                governorateIds[0], $"محافظة متزامنة {suffix}", $"Governorate A {suffix}", 1).Value,
            Governorate.Create(
                governorateIds[1], $"محافظة متزامنة {suffix}", $"Governorate B {suffix}", 2).Value,
            "Governorate.DuplicateNameAr");
        await AssertOneStableConflictAsync(
            City.Create(
                cityIds[0], governorateParentId, $"مدينة متزامنة {suffix}", $"City A {suffix}", 1).Value,
            City.Create(
                cityIds[1], governorateParentId, $"مدينة متزامنة {suffix}", $"City B {suffix}", 2).Value,
            "City.DuplicateNameAr");
        await AssertOneStableConflictAsync(
            Area.Create(
                areaIds[0], cityParentId, $"منطقة متزامنة {suffix}", $"Area A {suffix}", 1).Value,
            Area.Create(
                areaIds[1], cityParentId, $"منطقة متزامنة {suffix}", $"Area B {suffix}", 2).Value,
            "Area.DuplicateNameAr");
        await AssertOneStableConflictAsync(
            LegalSpecialization.Create(
                specializationIds[0], $"تخصص متزامن {suffix}", $"Specialization A {suffix}", 1).Value,
            LegalSpecialization.Create(
                specializationIds[1], $"تخصص متزامن {suffix}", $"Specialization B {suffix}", 2).Value,
            "LegalSpecialization.DuplicateNameAr");
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConsultationReferenceUniqueIndexIsAuthoritativeAndCreationPersistenceRecognizesItsCollision()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var referenceNumber = $"AV-{suffix[..6].Replace('0', '2').Replace('1', '3').ToUpperInvariant()}";
        var account = UserAccount.CreateLawyer(
            $"reference-{suffix[..8]}",
            $"REFERENCE-{suffix[..8]}",
            $"reference-{suffix}@example.test",
            $"REFERENCE-{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
            $"01{suffix[..18]}",
            "integration-test-hash",
            DateTime.UtcNow).Value;
        var lawyer = LawyerProfile.Create(account, "Reference Collision Lawyer").Value;
        var existing = CreateGuestRequest(referenceNumber, lawyer.Id, "01060000001");

        await using (var seedContext = fixture.CreateContext())
        {
            seedContext.AddRange(account, lawyer, existing);
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var retryContext = fixture.CreateContext())
        {
            var persistence = new ConsultationRequestCreationPersistence(retryContext);
            Assert.False(await persistence.TryAddAsync(
                CreateGuestRequest(referenceNumber, lawyer.Id, "01060000002"),
                TestContext.Current.CancellationToken));
        }

        await using var duplicateContext = fixture.CreateContext();
        duplicateContext.ConsultationRequests.Add(
            CreateGuestRequest(referenceNumber, lawyer.Id, "01060000003"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            duplicateContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.True(new LawyerPlatformUniqueConstraintExceptionMapper().TryMap(exception, out var error));
        Assert.Equal("ConsultationRequest.ReferenceNumberConflict", error.Code);
    }

    private async Task AssertOneStableConflictAsync<T>(T first, T second, string expectedCode)
        where T : class
    {
        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        firstContext.Add(first);
        secondContext.Add(second);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstSave = CaptureSaveAsync(firstContext, gate.Task);
        var secondSave = CaptureSaveAsync(secondContext, gate.Task);
        gate.SetResult();
        var results = await Task.WhenAll(firstSave, secondSave);

        Assert.Single(results, result => result is null);
        var exception = Assert.IsType<DbUpdateException>(Assert.Single(results, result => result is not null));
        Assert.True(new LawyerPlatformUniqueConstraintExceptionMapper().TryMap(exception, out var error));
        Assert.Equal(expectedCode, error.Code);
    }

    private static async Task<Exception?> CaptureSaveAsync(
        LawyerPlatformDbContext context,
        Task gate)
    {
        await gate;
        try
        {
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static ConsultationRequest CreateGuestRequest(
        string referenceNumber,
        Guid lawyerId,
        string phoneNumber)
        => ConsultationRequest.CreateForGuest(
            referenceNumber,
            "SQL Server Guest",
            phoneNumber,
            null,
            lawyerId,
            ConsultationType.Online,
            null,
            "SQL Server collision test",
            null,
            DateTime.UtcNow,
            500m).Value;
}
