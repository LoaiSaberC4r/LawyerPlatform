namespace LawyerPlatform.Infrastructure.Seeding;

internal enum LegalSpecializationSeedConflictKind
{
    Id = 1,
    ArabicName = 2,
    EnglishName = 3
}

internal sealed class LegalSpecializationSeedConflictException(
    LegalSpecializationSeedConflictKind conflictKind,
    int seedId,
    string message)
    : InvalidOperationException(message)
{
    public LegalSpecializationSeedConflictKind ConflictKind { get; } = conflictKind;
    public int SeedId { get; } = seedId;
}
