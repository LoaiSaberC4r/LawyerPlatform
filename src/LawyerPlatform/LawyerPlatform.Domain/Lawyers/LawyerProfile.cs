using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Domain.Lawyers;

public sealed class LawyerProfile : AggregateRoot<Guid>, IAuditableEntity, ISoftDeleteEntity
{
    private LawyerProfile()
    {
    }

    private LawyerProfile(Guid id, UserAccount userAccount, string fullName)
        : base(id)
    {
        UserAccountId = userAccount.Id;
        UserAccount = userAccount;
        FullName = fullName.Trim();
        ApprovalStatus = LawyerApprovalStatus.Draft;
    }

    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;
    public string FullName { get; private set; } = string.Empty;
    public string? ProfessionalTitle { get; private set; }
    public string? Biography { get; private set; }
    public int? YearsOfExperience { get; private set; }
    public string? ProfessionalRegistrationNumber { get; private set; }
    public string? ProfileImageStorageKey { get; private set; }
    public LawyerApprovalStatus ApprovalStatus { get; private set; }
    public string? ApprovalReason { get; private set; }
    public DateTime? ApprovedOnUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? SuspendedOnUtc { get; private set; }
    public string? SuspensionReason { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public DateTime? RestoredOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<LawyerProfile> Create(UserAccount userAccount, string fullName)
    {
        ArgumentNullException.ThrowIfNull(userAccount);

        if (userAccount.Role != AccountRole.Lawyer)
        {
            return Result<LawyerProfile>.Fail(Error.Domain("LawyerProfile.InvalidAccountRole", "A lawyer profile requires a lawyer account."));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result<LawyerProfile>.Fail(AccountErrors.FullNameRequired);
        }

        return Result<LawyerProfile>.Ok(new LawyerProfile(Guid.NewGuid(), userAccount, fullName));
    }
}
