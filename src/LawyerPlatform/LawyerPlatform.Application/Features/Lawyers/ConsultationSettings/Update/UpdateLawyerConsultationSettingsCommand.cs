using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Features.Lawyers.ConsultationSettings.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.ConsultationSettings.Update;

public sealed record UpdateLawyerAvailabilityItem(
    string DayOfWeek,
    TimeOnly? StartTime,
    TimeOnly? EndTime);

public sealed record UpdateLawyerConsultationSettingsCommand(
    decimal ConsultationPrice,
    IReadOnlyList<UpdateLawyerAvailabilityItem>? Availability,
    string? RowVersion)
    : ICommand<LawyerConsultationSettingsResponse>,
      ITransactionalCommand<LawyerPlatformWritePersistence>;

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

        var periodsResult = CreatePeriods(command.Availability);
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
                command.ConsultationPrice,
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
            var update = settings.Update(command.ConsultationPrice, periodsResult.Value);
            if (update.IsFailure)
            {
                return Result<LawyerConsultationSettingsResponse>.Fail(update.Errors);
            }

            // Touch one aggregate-root scalar so availability-only and no-op PUTs advance
            // the independent settings RowVersion without changing child entity states.
            concurrencyTokenManager.MarkPropertyModified(
                settings,
                item => item.ConsultationPrice);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<LawyerConsultationSettingsResponse>.Ok(
            LawyerConsultationSettingsMapper.Map(settings));
    }

    private static Result<IReadOnlyCollection<LawyerAvailabilityPeriod>> CreatePeriods(
        IReadOnlyList<UpdateLawyerAvailabilityItem>? items)
    {
        if (items is null)
        {
            return Result<IReadOnlyCollection<LawyerAvailabilityPeriod>>.Fail(
                LawyerErrors.AvailabilityInvalid);
        }

        var periods = new List<LawyerAvailabilityPeriod>(items.Count);
        foreach (var item in items)
        {
            if (!TryParseDay(item.DayOfWeek, out var day) ||
                !item.StartTime.HasValue ||
                !item.EndTime.HasValue)
            {
                return Result<IReadOnlyCollection<LawyerAvailabilityPeriod>>.Fail(
                    LawyerErrors.AvailabilityInvalid);
            }

            periods.Add(new LawyerAvailabilityPeriod(day, item.StartTime.Value, item.EndTime.Value));
        }

        return Result<IReadOnlyCollection<LawyerAvailabilityPeriod>>.Ok(periods);
    }

    private static bool TryParseDay(string? value, out DayOfWeek dayOfWeek)
        => Enum.TryParse(value, ignoreCase: true, out dayOfWeek) && Enum.IsDefined(dayOfWeek);
}
