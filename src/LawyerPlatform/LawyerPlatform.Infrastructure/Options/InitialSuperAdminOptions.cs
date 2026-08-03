namespace LawyerPlatform.Infrastructure.Options;

public sealed class InitialSuperAdminOptions
{
    public const string SectionName = "InitialSuperAdmin";

    public bool Enabled { get; init; }
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
