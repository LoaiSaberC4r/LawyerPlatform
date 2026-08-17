using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Notifications.Email;

internal sealed record ConsultationCreationEmailContext(
    Guid LawyerUserAccountId,
    string LawyerName,
    string LawyerEmail,
    string RequesterName,
    string? RequesterEmail,
    string RequesterIdentity,
    string SpecializationNameAr,
    string SpecializationNameEn);

internal sealed class EmailNotificationCoordinator(
    IEmailNotificationFactory factory,
    IEmailNotificationOutbox outbox,
    IReadRepository<UserAccount, LawyerPlatformReadPersistence> accountReader,
    IReadRepository<ConsultationRequest, LawyerPlatformReadPersistence> consultationReader)
{
    public async Task QueueLawyerTransitionAsync(
        LawyerProfile profile,
        LawyerApprovalStatusHistory history,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(history);

        var notificationType = (history.OldStatus, history.NewStatus) switch
        {
            (LawyerApprovalStatus.Draft or LawyerApprovalStatus.ChangesRequested,
                LawyerApprovalStatus.PendingApproval) => EmailNotificationType.LawyerSubmittedForApproval,
            (LawyerApprovalStatus.PendingApproval, LawyerApprovalStatus.Approved) => EmailNotificationType.LawyerApproved,
            (LawyerApprovalStatus.PendingApproval, LawyerApprovalStatus.ChangesRequested) => EmailNotificationType.LawyerChangesRequested,
            (LawyerApprovalStatus.PendingApproval, LawyerApprovalStatus.Rejected) => EmailNotificationType.LawyerRejected,
            (LawyerApprovalStatus.Approved, LawyerApprovalStatus.Suspended) => EmailNotificationType.LawyerSuspended,
            (LawyerApprovalStatus.Suspended, LawyerApprovalStatus.Approved) => EmailNotificationType.LawyerReactivated,
            _ => (EmailNotificationType?)null
        };
        if (notificationType is null)
        {
            return;
        }

        var model = new LawyerEmailNotificationModel(profile.FullName, profile.Id, history.Reason);
        if (notificationType == EmailNotificationType.LawyerSubmittedForApproval)
        {
            var administrators = await accountReader.ListAsync(
                new ActiveSuperAdminEmailRecipientsSpecification(),
                cancellationToken);
            foreach (var administrator in administrators
                         .GroupBy(item => item.Email, StringComparer.OrdinalIgnoreCase)
                         .Select(group => group.First()))
            {
                await QueueAsync(
                    notificationType.Value,
                    profile.Id,
                    history.Id.ToString("N"),
                    administrator.Id.ToString("N"),
                    administrator.Email,
                    model,
                    cancellationToken);
            }

            return;
        }

        await QueueAsync(
            notificationType.Value,
            profile.Id,
            history.Id.ToString("N"),
            profile.UserAccountId.ToString("N"),
            profile.UserAccount.Email,
            model,
            cancellationToken);
    }

    public async Task QueueConsultationCreatedAsync(
        ConsultationRequest request,
        ConsultationCreationEmailContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var model = new ConsultationEmailNotificationModel(
            context.RequesterName,
            context.LawyerName,
            request.ReferenceNumber,
            context.SpecializationNameAr,
            context.SpecializationNameEn,
            request.PreferredAppointmentOnUtc);
        var eventId = request.Id.ToString("N");

        await QueueAsync(
            EmailNotificationType.ConsultationRequestCreatedForLawyer,
            request.Id,
            eventId,
            context.LawyerUserAccountId.ToString("N"),
            context.LawyerEmail,
            model,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(context.RequesterEmail))
        {
            await QueueAsync(
                EmailNotificationType.ConsultationRequestCreatedConfirmation,
                request.Id,
                eventId,
                context.RequesterIdentity,
                context.RequesterEmail,
                model,
                cancellationToken);
        }
    }

    public async Task QueueConsultationTransitionAsync(
        ConsultationRequest request,
        ConsultationRequestStatusHistory history,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(history);

        var notificationType = (history.OldStatus, history.NewStatus) switch
        {
            (ConsultationRequestStatus.New, ConsultationRequestStatus.UnderReview) => EmailNotificationType.ConsultationUnderReview,
            (ConsultationRequestStatus.New or ConsultationRequestStatus.UnderReview, ConsultationRequestStatus.Approved) => EmailNotificationType.ConsultationApproved,
            (ConsultationRequestStatus.New or ConsultationRequestStatus.UnderReview, ConsultationRequestStatus.Rejected) => EmailNotificationType.ConsultationRejected,
            (ConsultationRequestStatus.Approved, ConsultationRequestStatus.Completed) => EmailNotificationType.ConsultationCompleted,
            _ => (EmailNotificationType?)null
        };
        if (notificationType is null)
        {
            return;
        }

        var snapshot = await consultationReader.FirstOrDefaultAsync(
            new ConsultationEmailSnapshotByIdSpecification(request.Id),
            cancellationToken);
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.RequesterEmail))
        {
            return;
        }

        var model = new ConsultationEmailNotificationModel(
            snapshot.RequesterName,
            snapshot.LawyerName,
            snapshot.ReferenceNumber,
            snapshot.SpecializationNameAr,
            snapshot.SpecializationNameEn,
            snapshot.PreferredAppointmentOnUtc,
            history.Reason);
        await QueueAsync(
            notificationType.Value,
            request.Id,
            history.Id.ToString("N"),
            snapshot.RequesterIdentity,
            snapshot.RequesterEmail,
            model,
            cancellationToken);
    }

    public Task QueueClientTransitionAsync(
        LawyerPlatform.Domain.Clients.ClientProfile profile,
        AccountStatus oldStatus,
        AccountStatus newStatus,
        DateTime changedOnUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var notificationType = (oldStatus, newStatus) switch
        {
            (AccountStatus.Active, AccountStatus.Suspended) => EmailNotificationType.ClientSuspended,
            (AccountStatus.Suspended, AccountStatus.Active) => EmailNotificationType.ClientReactivated,
            _ => (EmailNotificationType?)null
        };
        return notificationType is null
            ? Task.CompletedTask
            : QueueAsync(
                notificationType.Value,
                profile.Id,
                changedOnUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
                profile.UserAccountId.ToString("N"),
                profile.UserAccount.Email,
                new ClientEmailNotificationModel(profile.FullName),
                cancellationToken);
    }

    private Task QueueAsync(
        EmailNotificationType notificationType,
        Guid aggregateId,
        string eventId,
        string recipientIdentity,
        string recipientEmail,
        EmailNotificationModel model,
        CancellationToken cancellationToken)
    {
        var content = factory.Create(notificationType, model);
        var idempotencyKey = $"{notificationType}:{aggregateId:N}:{eventId}:{recipientIdentity}";
        return outbox.QueueAsync(
            new QueueEmailNotification(
                notificationType,
                aggregateId,
                idempotencyKey,
                recipientEmail,
                content.Subject,
                content.HtmlBody),
            cancellationToken);
    }
}

internal sealed record EmailRecipientSnapshot(Guid Id, string Email);

internal sealed class ActiveSuperAdminEmailRecipientsSpecification
    : Specification<UserAccount, EmailRecipientSnapshot>
{
    public ActiveSuperAdminEmailRecipientsSpecification()
    {
        AddCriteria(account =>
            account.Role == AccountRole.SuperAdmin &&
            account.Status == AccountStatus.Active);
        UseNoTracking();
        Select(account => new EmailRecipientSnapshot(account.Id, account.Email));
    }
}

internal sealed record ConsultationEmailSnapshot(
    string ReferenceNumber,
    string RequesterName,
    string? RequesterEmail,
    string RequesterIdentity,
    string LawyerName,
    string SpecializationNameAr,
    string SpecializationNameEn,
    DateTime? PreferredAppointmentOnUtc);

internal sealed class ConsultationEmailSnapshotByIdSpecification
    : Specification<ConsultationRequest, ConsultationEmailSnapshot>
{
    public ConsultationEmailSnapshotByIdSpecification(Guid requestId)
    {
        AddCriteria(request => request.Id == requestId);
        UseNoTracking();
        Select(request => new ConsultationEmailSnapshot(
            request.ReferenceNumber,
            request.ClientProfileId != null ? request.ClientProfile!.FullName : request.GuestFullName!,
            request.ClientProfileId != null ? request.ClientProfile!.UserAccount.Email : request.GuestEmail,
            request.ClientProfileId != null
                ? request.ClientProfileId.Value.ToString()
                : request.Id.ToString(),
            request.LawyerProfile.FullName,
            request.LegalSpecialization == null ? "غير محدد" : request.LegalSpecialization.NameAr,
            request.LegalSpecialization == null ? "Not specified" : request.LegalSpecialization.NameEn,
            request.PreferredAppointmentOnUtc));
    }
}
