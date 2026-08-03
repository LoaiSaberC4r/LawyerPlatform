namespace LawyerPlatform.Infrastructure.Options;

public sealed class DatabaseInitializationOptions
{
    public const string SectionName = "DatabaseInitialization";

    public bool ApplyMigrationsOnStartup { get; init; } = true;
}
