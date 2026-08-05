namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed class EgyptLocationSeedDataException : InvalidOperationException
{
    public EgyptLocationSeedDataException(string message)
        : base(message)
    {
    }

    public EgyptLocationSeedDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
