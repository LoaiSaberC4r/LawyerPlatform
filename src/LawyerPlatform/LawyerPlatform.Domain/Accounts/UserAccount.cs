using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.Accounts;

public sealed class UserAccount : AggregateRoot<Guid>, IAuditableEntity
{
    private UserAccount()
    {
    }

    private UserAccount(
        Guid id,
        string userName,
        string normalizedUserName,
        string email,
        string normalizedEmail,
        string phoneNumber,
        string passwordHash,
        AccountRole role,
        bool isFirstLogin,
        DateTime nowUtc)
        : base(id)
    {
        UserName = userName.Trim();
        NormalizedUserName = normalizedUserName;
        Email = email.Trim();
        NormalizedEmail = normalizedEmail;
        PhoneNumber = phoneNumber.Trim();
        PasswordHash = passwordHash;
        Role = role;
        Status = AccountStatus.Active;
        IsFirstLogin = isFirstLogin;
        PasswordChangedOnUtc = nowUtc;
    }

    public string UserName { get; private set; } = string.Empty;
    public string NormalizedUserName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public AccountRole Role { get; private set; }
    public AccountStatus Status { get; private set; }
    public bool IsFirstLogin { get; private set; }
    public DateTime PasswordChangedOnUtc { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<UserAccount> CreateSuperAdmin(
        Guid id,
        string userName,
        string normalizedUserName,
        string email,
        string normalizedEmail,
        string phoneNumber,
        string passwordHash,
        DateTime nowUtc)
        => Create(id, userName, normalizedUserName, email, normalizedEmail, phoneNumber, passwordHash, AccountRole.SuperAdmin, true, nowUtc);

    public static Result<UserAccount> CreateLawyer(
        string userName,
        string normalizedUserName,
        string email,
        string normalizedEmail,
        string phoneNumber,
        string passwordHash,
        DateTime nowUtc)
        => Create(Guid.NewGuid(), userName, normalizedUserName, email, normalizedEmail, phoneNumber, passwordHash, AccountRole.Lawyer, false, nowUtc);

    public static Result<UserAccount> CreateClient(
        string userName,
        string normalizedUserName,
        string email,
        string normalizedEmail,
        string phoneNumber,
        string passwordHash,
        DateTime nowUtc)
        => Create(Guid.NewGuid(), userName, normalizedUserName, email, normalizedEmail, phoneNumber, passwordHash, AccountRole.Client, false, nowUtc);

    public Result ChangePassword(string passwordHash, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Fail(AccountErrors.PasswordHashRequired);
        }

        PasswordHash = passwordHash;
        PasswordChangedOnUtc = nowUtc;
        IsFirstLogin = false;
        ModifiedOnUtc = nowUtc;
        return Result.Ok();
    }

    public Result Suspend(DateTime nowUtc)
    {
        if (Status != AccountStatus.Active)
        {
            return Result.Fail(AccountErrors.InvalidStatusTransition);
        }

        Status = AccountStatus.Suspended;
        ModifiedOnUtc = nowUtc;
        return Result.Ok();
    }

    public Result Reactivate(DateTime nowUtc)
    {
        if (Status != AccountStatus.Suspended)
        {
            return Result.Fail(AccountErrors.InvalidStatusTransition);
        }

        Status = AccountStatus.Active;
        ModifiedOnUtc = nowUtc;
        return Result.Ok();
    }

    public Result Deactivate(DateTime nowUtc)
    {
        if (Status != AccountStatus.Active)
        {
            return Result.Fail(AccountErrors.InvalidStatusTransition);
        }

        Status = AccountStatus.Inactive;
        ModifiedOnUtc = nowUtc;
        return Result.Ok();
    }

    private static Result<UserAccount> Create(
        Guid id,
        string userName,
        string normalizedUserName,
        string email,
        string normalizedEmail,
        string phoneNumber,
        string passwordHash,
        AccountRole role,
        bool isFirstLogin,
        DateTime nowUtc)
    {
        var errors = new List<Error>();
        if (id == Guid.Empty)
        {
            errors.Add(Error.Validation("Account.IdRequired", "Account id is required."));
        }

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(normalizedUserName))
        {
            errors.Add(AccountErrors.UserNameRequired);
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(normalizedEmail))
        {
            errors.Add(AccountErrors.EmailRequired);
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            errors.Add(AccountErrors.PhoneNumberRequired);
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            errors.Add(AccountErrors.PasswordHashRequired);
        }

        return errors.Count > 0
            ? Result<UserAccount>.Fail(errors)
            : Result<UserAccount>.Ok(new UserAccount(id, userName, normalizedUserName, email, normalizedEmail, phoneNumber, passwordHash, role, isFirstLogin, nowUtc));
    }
}
