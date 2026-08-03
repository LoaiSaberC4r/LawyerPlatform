using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Domain.Clients;

public sealed class ClientProfile : AggregateRoot<Guid>, IAuditableEntity, ISoftDeleteEntity
{
    private ClientProfile()
    {
    }

    private ClientProfile(Guid id, UserAccount userAccount, string fullName)
        : base(id)
    {
        UserAccountId = userAccount.Id;
        UserAccount = userAccount;
        FullName = fullName.Trim();
    }

    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;
    public string FullName { get; private set; } = string.Empty;
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public DateTime? RestoredOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<ClientProfile> Create(UserAccount userAccount, string fullName)
    {
        ArgumentNullException.ThrowIfNull(userAccount);

        if (userAccount.Role != AccountRole.Client)
        {
            return Result<ClientProfile>.Fail(Error.Domain("ClientProfile.InvalidAccountRole", "A client profile requires a client account."));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result<ClientProfile>.Fail(AccountErrors.FullNameRequired);
        }

        return Result<ClientProfile>.Ok(new ClientProfile(Guid.NewGuid(), userAccount, fullName));
    }
}
