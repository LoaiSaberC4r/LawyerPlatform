using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Clients;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.UnitTests.Accounts;

public sealed class UserAccountTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateSuperAdmin_SetsApprovedInitialState()
    {
        var result = CreateSuperAdmin();

        Assert.True(result.IsSuccess);
        Assert.Equal(AccountRole.SuperAdmin, result.Value.Role);
        Assert.Equal(AccountStatus.Active, result.Value.Status);
        Assert.True(result.Value.IsFirstLogin);
        Assert.Equal(NowUtc, result.Value.PasswordChangedOnUtc);
    }

    [Fact]
    public void CreateLawyer_AndProfile_SetDraftWithoutInitialPasswordChange()
    {
        var account = UserAccount.CreateLawyer(
            "lawyer.one", "LAWYER.ONE", "lawyer@example.test", "LAWYER@EXAMPLE.TEST",
            "01000000001", "hash", NowUtc);
        var profile = LawyerProfile.Create(account.Value, "Lawyer One");

        Assert.True(account.IsSuccess);
        Assert.False(account.Value.IsFirstLogin);
        Assert.Equal(AccountRole.Lawyer, account.Value.Role);
        Assert.True(profile.IsSuccess);
        Assert.Equal(LawyerApprovalStatus.Draft, profile.Value.ApprovalStatus);
    }

    [Fact]
    public void CreateClient_AndProfile_SetClientWithoutInitialPasswordChange()
    {
        var account = UserAccount.CreateClient(
            "client.one", "CLIENT.ONE", "client@example.test", "CLIENT@EXAMPLE.TEST",
            "01000000002", "hash", NowUtc);
        var profile = ClientProfile.Create(account.Value, "Client One");

        Assert.True(account.IsSuccess);
        Assert.False(account.Value.IsFirstLogin);
        Assert.Equal(AccountRole.Client, account.Value.Role);
        Assert.True(profile.IsSuccess);
    }

    [Fact]
    public void ChangePassword_ClearsFirstLoginAndUpdatesTimestamps()
    {
        var account = CreateSuperAdmin().Value;
        var changedOnUtc = NowUtc.AddMinutes(5);

        var result = account.ChangePassword("new-hash", changedOnUtc);

        Assert.True(result.IsSuccess);
        Assert.False(account.IsFirstLogin);
        Assert.Equal("new-hash", account.PasswordHash);
        Assert.Equal(changedOnUtc, account.PasswordChangedOnUtc);
        Assert.Equal(changedOnUtc, account.ModifiedOnUtc);
    }

    [Fact]
    public void ChangePassword_RejectsEmptyHash()
    {
        var account = CreateSuperAdmin().Value;

        var result = account.ChangePassword(" ", NowUtc.AddMinutes(1));

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "Account.PasswordHashRequired");
        Assert.True(account.IsFirstLogin);
    }

    [Fact]
    public void AccountStatusTransitions_UpdateStatus()
    {
        var account = CreateSuperAdmin().Value;

        account.Suspend(NowUtc.AddMinutes(1));
        Assert.Equal(AccountStatus.Suspended, account.Status);
        account.Reactivate(NowUtc.AddMinutes(2));
        Assert.Equal(AccountStatus.Active, account.Status);
        account.Deactivate(NowUtc.AddMinutes(3));
        Assert.Equal(AccountStatus.Inactive, account.Status);
    }

    private static BuildingBlock.Domain.Results.Result<UserAccount> CreateSuperAdmin()
        => UserAccount.CreateSuperAdmin(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "superadmin", "SUPERADMIN", "admin@example.test", "ADMIN@EXAMPLE.TEST",
            "01000000000", "hash", NowUtc);
}
