using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Clients;

namespace LawyerPlatform.UnitTests.Clients;

public sealed class ClientProfileTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void UpdateFullName_WithValidName_SucceedsAndTrims()
    {
        var profile = CreateProfile();

        var result = profile.UpdateFullName("  Ahmed Mohamed Ali  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Ahmed Mohamed Ali", profile.FullName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateFullName_WithMissingName_Fails(string fullName)
    {
        var profile = CreateProfile();

        var result = profile.UpdateFullName(fullName);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "Account.FullNameRequired");
        Assert.Equal("Original Name", profile.FullName);
    }

    [Fact]
    public void UpdateFullName_WithMoreThanTwoHundredCharacters_Fails()
    {
        var profile = CreateProfile();

        var result = profile.UpdateFullName(new string('a', 201));

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "Account.FullNameTooLong");
        Assert.Equal("Original Name", profile.FullName);
    }

    private static ClientProfile CreateProfile()
    {
        var account = UserAccount.CreateClient(
            "client.profile",
            "CLIENT.PROFILE",
            "client.profile@example.test",
            "CLIENT.PROFILE@EXAMPLE.TEST",
            "01012345678",
            "hash",
            NowUtc).Value;
        return ClientProfile.Create(account, "Original Name").Value;
    }
}
