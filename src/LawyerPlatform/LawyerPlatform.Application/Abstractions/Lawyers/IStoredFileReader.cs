namespace LawyerPlatform.Application.Abstractions.Lawyers;

public sealed record StoredFileContent(
    Stream Content,
    string ContentType,
    string FileName,
    long Length);

public interface IStoredFileReader
{
    Task<StoredFileContent?> OpenReadAsync(
        string storageKey,
        string downloadFileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
