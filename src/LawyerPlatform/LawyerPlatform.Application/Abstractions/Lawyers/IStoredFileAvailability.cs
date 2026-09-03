namespace LawyerPlatform.Application.Abstractions.Lawyers;

public interface IStoredFileAvailability
{
    Task<bool> ExistsAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
