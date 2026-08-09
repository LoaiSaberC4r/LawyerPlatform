using LawyerPlatform.Application.Features.AdminClients.GetClients;
using LawyerPlatform.Application.Features.AdminClients.ReactivateClient;
using LawyerPlatform.Application.Features.AdminClients.SuspendClient;
using LawyerPlatform.Application.Features.Clients.Profile;

namespace LawyerPlatform.UnitTests.Clients;

public sealed class ClientValidatorTests
{
    private static readonly string ValidRowVersion = Convert.ToBase64String(new byte[8]);

    [Fact]
    public void UpdateOwnProfile_RejectsMissingLongNameAndInvalidRowVersion()
    {
        var missing = new UpdateOwnClientProfileCommandValidator().Validate(
            new UpdateOwnClientProfileCommand("   ", "not-base64"));
        var tooLong = new UpdateOwnClientProfileCommandValidator().Validate(
            new UpdateOwnClientProfileCommand(new string('a', 201), ValidRowVersion));

        Assert.False(missing.IsValid);
        Assert.Contains(missing.Errors, error => error.ErrorCode == "Account.FullNameRequired");
        Assert.Contains(missing.Errors, error => error.ErrorCode == "Client.InvalidRowVersion");
        Assert.False(tooLong.IsValid);
        Assert.Contains(tooLong.Errors, error => error.ErrorCode == "Account.FullNameTooLong");
    }

    [Fact]
    public void AdminClientPaging_RequiresPositiveBoundedValues()
    {
        var result = new GetAdminClientsQueryValidator().Validate(new GetAdminClientsQuery(0, 101));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GetAdminClientsQuery.PageNumber));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GetAdminClientsQuery.PageSize));
    }

    [Fact]
    public void AccountLifecycleCommands_RequireClientIdAndValidAccountRowVersion()
    {
        var suspend = new SuspendClientCommandValidator().Validate(
            new SuspendClientCommand(Guid.Empty, "invalid"));
        var reactivate = new ReactivateClientCommandValidator().Validate(
            new ReactivateClientCommand(Guid.Empty, "invalid"));

        Assert.False(suspend.IsValid);
        Assert.False(reactivate.IsValid);
        Assert.Contains(suspend.Errors, error => error.ErrorCode == "Account.InvalidRowVersion");
        Assert.Contains(reactivate.Errors, error => error.ErrorCode == "Account.InvalidRowVersion");
    }
}
