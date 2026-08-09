using LawyerPlatform.Domain.ReferenceData;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LawyerPlatform.IntegrationTests.Seeding;

public sealed class LegalSpecializationSeedingTests
{
    [Fact]
    public async Task EmptyDatabaseReceivesAllApprovedActiveRecords()
    {
        await using var factory = new CustomWebApplicationFactory();

        await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        var stored = await context.LegalSpecializations
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SeedCatalog.ExpectedLegalSpecializationCount, stored.Count);
        Assert.Equal(
            SeedCatalog.LegalSpecializations.Select(seed =>
                (seed.Id, seed.NameAr, seed.NameEn, seed.DisplayOrder)),
            stored.Select(item =>
                (item.Id, item.NameAr, item.NameEn, item.DisplayOrder)));
        Assert.All(stored, item => Assert.True(item.IsActive));
    }

    [Fact]
    public async Task SecondExecutionCreatesNoDuplicateRows()
    {
        await using var factory = new CustomWebApplicationFactory();

        await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);
        await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        Assert.Equal(
            SeedCatalog.ExpectedLegalSpecializationCount,
            await context.LegalSpecializations.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            SeedCatalog.ExpectedLegalSpecializationCount,
            await context.LegalSpecializations
                .Select(item => item.Id)
                .Distinct()
                .CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExistingSuperAdminChangesAreNotOverwritten()
    {
        await using var factory = new CustomWebApplicationFactory();
        var seed = SeedCatalog.LegalSpecializations[0];
        const string modifiedArabicName = "قضايا الأسرة المعدلة إداريا";
        const string modifiedEnglishName = "SuperAdmin edited family law";
        const int modifiedDisplayOrder = 777;
        await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);
        await ExecuteSqlAsync(
            factory,
            $"UPDATE LegalSpecializations SET NameAr = {modifiedArabicName}, NameEn = {modifiedEnglishName}, DisplayOrder = {modifiedDisplayOrder} WHERE Id = {seed.Id}");

        await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        var stored = await context.LegalSpecializations
            .AsNoTracking()
            .SingleAsync(item => item.Id == seed.Id, TestContext.Current.CancellationToken);
        Assert.Equal(modifiedArabicName, stored.NameAr);
        Assert.Equal(modifiedEnglishName, stored.NameEn);
        Assert.Equal(modifiedDisplayOrder, stored.DisplayOrder);
    }

    [Fact]
    public async Task DeactivatedSeedRecordIsNotReactivated()
    {
        await using var factory = new CustomWebApplicationFactory();
        var seed = SeedCatalog.LegalSpecializations[0];
        await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);
        await ExecuteSqlAsync(
            factory,
            $"UPDATE LegalSpecializations SET IsActive = 0 WHERE Id = {seed.Id}");

        await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        Assert.False(await context.LegalSpecializations
            .Where(item => item.Id == seed.Id)
            .Select(item => item.IsActive)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnrelatedExistingRecordUsingApprovedIdIsRejected()
    {
        await using var factory = new CustomWebApplicationFactory();
        var seed = SeedCatalog.LegalSpecializations[0];
        await AddSpecializationAsync(
            factory,
            LegalSpecialization.Create(seed.Id, "تخصص غير مرتبط", "Unrelated specialization", 999).Value);

        var exception = await Assert.ThrowsAsync<LegalSpecializationSeedConflictException>(
            () => factory.SeedDatabaseAsync(TestContext.Current.CancellationToken));

        Assert.Equal(LegalSpecializationSeedConflictKind.Id, exception.ConflictKind);
        Assert.Equal(seed.Id, exception.SeedId);
    }

    [Fact]
    public async Task ApprovedArabicNameOwnedByAnotherRecordIsRejected()
    {
        await using var factory = new CustomWebApplicationFactory();
        var seed = SeedCatalog.LegalSpecializations[0];
        await AddSpecializationAsync(
            factory,
            LegalSpecialization.Create(1000, seed.NameAr, "Different English Name", 999).Value);

        var exception = await Assert.ThrowsAsync<LegalSpecializationSeedConflictException>(
            () => factory.SeedDatabaseAsync(TestContext.Current.CancellationToken));

        Assert.Equal(LegalSpecializationSeedConflictKind.ArabicName, exception.ConflictKind);
        Assert.Equal(seed.Id, exception.SeedId);
    }

    [Fact]
    public async Task ApprovedEnglishNameOwnedByAnotherRecordIsRejectedCaseInsensitively()
    {
        await using var factory = new CustomWebApplicationFactory();
        var seed = SeedCatalog.LegalSpecializations[0];
        await AddSpecializationAsync(
            factory,
            LegalSpecialization.Create(1000, "اسم عربي مختلف", seed.NameEn.ToUpperInvariant(), 999).Value);

        var exception = await Assert.ThrowsAsync<LegalSpecializationSeedConflictException>(
            () => factory.SeedDatabaseAsync(TestContext.Current.CancellationToken));

        Assert.Equal(LegalSpecializationSeedConflictKind.EnglishName, exception.ConflictKind);
        Assert.Equal(seed.Id, exception.SeedId);
    }

    private static async Task AddSpecializationAsync(
        CustomWebApplicationFactory factory,
        LegalSpecialization specialization)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        context.LegalSpecializations.Add(specialization);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task ExecuteSqlAsync(
        CustomWebApplicationFactory factory,
        FormattableString command)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync(
            command,
            TestContext.Current.CancellationToken);
    }
}
