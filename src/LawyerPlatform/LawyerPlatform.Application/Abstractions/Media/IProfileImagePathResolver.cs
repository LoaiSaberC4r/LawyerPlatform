namespace LawyerPlatform.Application.Abstractions.Media;

public interface IProfileImagePathResolver
{
    string? Resolve(string? profileImageStorageKey);
}
