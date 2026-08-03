namespace LawyerPlatform.Infrastructure.Options;

public sealed class PasswordLifecycleOptions
{
    public const string SectionName = "PasswordLifecycle";

    public int ExpiryDays { get; init; } = 90;
}
