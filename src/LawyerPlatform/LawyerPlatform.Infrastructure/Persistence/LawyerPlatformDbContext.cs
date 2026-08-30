using BuildingBlock.Infrastructure.Extensions;
using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Clients;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.ContactInquiries;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Domain.ReferenceData;
using LawyerPlatform.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LawyerPlatform.Infrastructure.Persistence;

public sealed class LawyerPlatformDbContext(DbContextOptions<LawyerPlatformDbContext> options)
    : DbContext(options)
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<PasswordResetChallenge> PasswordResetChallenges => Set<PasswordResetChallenge>();
    public DbSet<ClientProfile> ClientProfiles => Set<ClientProfile>();
    public DbSet<LawyerProfile> LawyerProfiles => Set<LawyerProfile>();
    public DbSet<Governorate> Governorates => Set<Governorate>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<LegalSpecialization> LegalSpecializations => Set<LegalSpecialization>();
    public DbSet<LawyerOffice> LawyerOffices => Set<LawyerOffice>();
    public DbSet<LawyerSpecialization> LawyerSpecializations => Set<LawyerSpecialization>();
    public DbSet<LawyerDocument> LawyerDocuments => Set<LawyerDocument>();
    public DbSet<LawyerApprovalStatusHistory> LawyerApprovalStatusHistory => Set<LawyerApprovalStatusHistory>();
    public DbSet<LawyerConsultationSettings> LawyerConsultationSettings => Set<LawyerConsultationSettings>();
    public DbSet<LawyerAvailability> LawyerAvailabilities => Set<LawyerAvailability>();
    public DbSet<ConsultationRequest> ConsultationRequests => Set<ConsultationRequest>();
    public DbSet<ConsultationRequestStatusHistory> ConsultationRequestStatusHistory => Set<ConsultationRequestStatusHistory>();
    public DbSet<ContactInquiry> ContactInquiries => Set<ContactInquiry>();
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareSqliteRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareSqliteRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyWriteConfigurations(typeof(LawyerPlatformDbContext).Assembly);
        modelBuilder.ApplySoftDeleteQueryFilter();

        if (string.Equals(Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
        {
            ConfigureSqliteConcurrencyFallback(modelBuilder);
        }
    }

    private static void ConfigureSqliteConcurrencyFallback(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersion = entityType.FindProperty("RowVersion");
            if (rowVersion is null)
            {
                continue;
            }

            rowVersion.ValueGenerated = ValueGenerated.Never;
            rowVersion.SetBeforeSaveBehavior(PropertySaveBehavior.Save);
            rowVersion.SetAfterSaveBehavior(PropertySaveBehavior.Save);
        }
    }

    private void PrepareSqliteRowVersions()
    {
        if (!string.Equals(Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            var rowVersion = entry.Metadata.FindProperty("RowVersion");
            if (rowVersion is not null)
            {
                entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray()[..8];
            }
        }
    }
}
