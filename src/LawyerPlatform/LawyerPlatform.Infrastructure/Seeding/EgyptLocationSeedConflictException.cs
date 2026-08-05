namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed class EgyptLocationSeedConflictException(string message)
    : InvalidOperationException(message);
