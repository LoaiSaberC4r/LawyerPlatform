using BuildingBlock.Application.Abstraction.Encryption;
using FluentValidation;
using LawyerPlatform.Application.Features.Auth.ChangePassword;
using LawyerPlatform.Application.Features.Auth.Login;
using LawyerPlatform.Application.Features.Auth.RegisterClient;
using LawyerPlatform.Application.Features.Auth.RegisterLawyer;

namespace LawyerPlatform.UnitTests.Auth;

public sealed class ValidatorTests
{
    private readonly IPasswordService _passwordService = new TestPasswordService();

    [Theory]
    [InlineData("valid.user", true)]
    [InlineData("ab", false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    [InlineData("user@example", false)]
    [InlineData("user name", false)]
    [InlineData("_user", false)]
    [InlineData("user.", false)]
    public void RegisterClient_ValidatesUserName(string userName, bool expectedValid)
    {
        var validator = new RegisterClientCommandValidator(_passwordService);
        var command = ValidClient() with { UserName = userName };

        var result = validator.Validate(command);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void RegisterClient_RejectsInvalidEmailMissingPhonePasswordAndName()
    {
        var validator = new RegisterClientCommandValidator(_passwordService);

        var result = validator.Validate(new RegisterClientCommand("", "valid.user", "invalid", "", ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterClientCommand.FullName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterClientCommand.Email));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterClientCommand.PhoneNumber));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterClientCommand.Password));
    }

    [Fact]
    public void RegisterLawyer_AcceptsValidInput()
    {
        var validator = new RegisterLawyerCommandValidator(_passwordService);
        var result = validator.Validate(new RegisterLawyerCommand(
            "Lawyer One", "lawyer.one", "lawyer@example.test", "01000000001", "StrongPassword1"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Login_RejectsMissingIdentifierAndPassword()
    {
        var result = new LoginQueryValidator().Validate(new LoginQuery("", ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LoginQuery.UserNameOrEmail));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LoginQuery.Password));
    }

    [Fact]
    public void ChangePassword_RejectsMissingAndWeakInput()
    {
        var validator = new ChangePasswordCommandValidator(_passwordService);

        var result = validator.Validate(new ChangePasswordCommand("", "weak"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ChangePasswordCommand.CurrentPassword));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ChangePasswordCommand.NewPassword));
    }

    private static RegisterClientCommand ValidClient()
        => new("Client One", "client.one", "client@example.test", "01000000001", "StrongPassword1");

    private sealed class TestPasswordService : IPasswordService
    {
        public string Hash(string password) => $"hash:{password}";
        public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
        public PasswordVerification VerifyDetailed(string password, string passwordHash) => new(Verify(password, passwordHash), false);
        public Task<string> HashAsync(string password, CancellationToken ct = default) => Task.FromResult(Hash(password));
        public Task<bool> VerifyAsync(string password, string passwordHash, CancellationToken ct = default) => Task.FromResult(Verify(password, passwordHash));
        public bool IsStrongPassword(string password) => password == "StrongPassword1";
    }
}
