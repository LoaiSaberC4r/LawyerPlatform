using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Features.Lawyers.ConsultationSettings.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.ConsultationSettings.Update;

public sealed record UpdateLawyerAvailabilityItem(
    string DayOfWeek,
    TimeOnly? StartTime,
    TimeOnly? EndTime);

public sealed record UpdateLawyerOnlineConsultationSettings(
    decimal Price,
    IReadOnlyList<UpdateLawyerAvailabilityItem>? Availability);

public sealed record UpdateLawyerOnsiteConsultationSettings(
    IReadOnlyList<UpdateLawyerAvailabilityItem>? Availability);

public sealed record UpdateLawyerConsultationSettingsCommand(
    UpdateLawyerOnlineConsultationSettings? Online,
    UpdateLawyerOnsiteConsultationSettings? Onsite,
    string? RowVersion)
    : ICommand<LawyerConsultationSettingsResponse>,
      ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class UpdateLawyerConsultationSettingsCommandValidator
    : AbstractValidator<UpdateLawyerConsultationSettingsCommand>
{
    public UpdateLawyerConsultationSettingsCommandValidator()
    {
        RuleFor(command => command.Online)
            .NotNull()
            .WithErrorCode("Lawyer.OnlineConsultationSettingsRequired");
        RuleFor(command => command.Onsite)
            .NotNull()
            .WithErrorCode("Lawyer.OnsiteConsultationSettingsRequired");
        When(command => command.Online is not null, () =>
        {
            RuleFor(command => command.Online!.Price)
                .GreaterThan(0)
                .WithErrorCode("Lawyer.ConsultationPriceInvalid")
                .LessThanOrEqualTo(LawyerConsultationSettings.MaximumConsultationPrice)
                .WithErrorCode("Lawyer.ConsultationPriceInvalid")
                .Must(price => decimal.Round(price, 2) == price)
                .WithErrorCode("Lawyer.ConsultationPriceInvalid");
            RuleFor(command => command.Online!.Availability)
                .NotNull()
                .WithErrorCode("Lawyer.AvailabilityInvalid");
        });
        When(command => command.Onsite is not null, () =>
            RuleFor(command => command.Onsite!.Availability)
                .NotNull()
                .WithErrorCode("Lawyer.AvailabilityInvalid"));
    }
}

internal sealed class UpdateLawyerConsultationSettingsCommandHandler(
    ICurrentUser currentUser,
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> lawyerReader,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager,
    IDateTimeProvider clock)
    : ICommandHandler<UpdateLawyerConsultationSettingsCommand, LawyerConsultationSettingsResponse>
{
    public async Task<Result<LawyerConsultationSettingsResponse>> Handle(
        UpdateLawyerConsultationSettingsCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userAccountId)
        {
            return Result<LawyerConsultationSettingsResponse>.Fail(LawyerErrors.NotFound);
        }

        var lawyer = await lawyerReader.FirstOrDefaultAsync(
            new CurrentLawyerIdSpecification(userAccountId),
            cancellationToken);
        if (lawyer is null)
        {
            return Result<LawyerConsultationSettingsResponse>.Fail(LawyerErrors.NotFound);
        }

        if (command.Online is null || command.Onsite is null)
        {
            return Result<LawyerConsultationSettingsResponse>.Fail(LawyerErrors.AvailabilityInvalid);
        }

        var periodsResult = CreatePeriods(command.Online.Availability, command.Onsite.Availability);
        if (periodsResult.IsFailure)
        {
            return Result<LawyerConsultationSettingsResponse>.Fail(periodsResult.Errors);
        }

        var repository = unitOfWork.WriteRepository<LawyerConsultationSettings>();
        var settings = await repository.FirstOrDefaultAsync(
            new LawyerConsultationSettingsForUpdateSpecification(lawyer.Id),
            cancellationToken);

        if (settings is null)
        {
            if (command.RowVersion is not null)
            {
                return Result<LawyerConsultationSettingsResponse>.Fail(
                    LawyerErrors.ConsultationSettingsInvalidRowVersion);
            }

            var creation = LawyerConsultationSettings.Create(
                lawyer.Id,
                command.Online.Price,
                periodsResult.Value,
                clock.UtcNow);
            if (creation.IsFailure)
            {
                return Result<LawyerConsultationSettingsResponse>.Fail(creation.Errors);
            }

            settings = creation.Value;
            await repository.AddAsync(settings, cancellationToken);
        }
        else
        {
            if (!RowVersionCodec.TryDecode(command.RowVersion, out var rowVersion))
            {
                return Result<LawyerConsultationSettingsResponse>.Fail(
                    LawyerErrors.ConsultationSettingsInvalidRowVersion);
            }

            concurrencyTokenManager.SetOriginalRowVersion(settings, rowVersion);
            var update = settings.Update(command.Online.Price, periodsResult.Value);
            if (update.IsFailure)
            {
                return Result<LawyerConsultationSettingsResponse>.Fail(update.Errors);
            }

            // Touch one aggregate-root scalar so availability-only and no-op PUTs advance
            // the independent settings RowVersion without changing child entity states.
            concurrencyTokenManager.MarkPropertyModified(
                settings,
                item => item.OnlineConsultationPrice);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<LawyerConsultationSettingsResponse>.Ok(
            LawyerConsultationSettingsMapper.Map(settings));
    }

    private static Result<IReadOnlyCollection<LawyerAvailabilityPeriod>> CreatePeriods(
        IReadOnlyList<UpdateLawyerAvailabilityItem>? onlineItems,
        IReadOnlyList<UpdateLawyerAvailabilityItem>? onsiteItems)
    {
        if (onlineItems is null || onsiteItems is null)
        {
            return Result<IReadOnlyCollection<LawyerAvailabilityPeriod>>.Fail(
                LawyerErrors.AvailabilityInvalid);
        }

        var periods = new List<LawyerAvailabilityPeriod>(onlineItems.Count + onsiteItems.Count);
        if (!AddPeriods(onlineItems, ConsultationType.Online, periods) ||
            !AddPeriods(onsiteItems, ConsultationType.Onsite, periods))
        {
            return Result<IReadOnlyCollection<LawyerAvailabilityPeriod>>.Fail(
                LawyerErrors.AvailabilityInvalid);
        }

        return Result<IReadOnlyCollection<LawyerAvailabilityPeriod>>.Ok(periods);
    }

    private static bool AddPeriods(
        IReadOnlyList<UpdateLawyerAvailabilityItem> items,
        ConsultationType consultationType,
        List<LawyerAvailabilityPeriod> periods)
    {
        foreach (var item in items)
        {
            if (!TryParseDay(item.DayOfWeek, out var day) ||
                !item.StartTime.HasValue ||
                !item.EndTime.HasValue)
            {
                return false;
            }

            periods.Add(new LawyerAvailabilityPeriod(
                consultationType,
                day,
                item.StartTime.Value,
                item.EndTime.Value));
        }

        return true;
    }

    private static bool TryParseDay(string? value, out DayOfWeek dayOfWeek)
        => Enum.TryParse(value, ignoreCase: true, out dayOfWeek) && Enum.IsDefined(dayOfWeek);
}
