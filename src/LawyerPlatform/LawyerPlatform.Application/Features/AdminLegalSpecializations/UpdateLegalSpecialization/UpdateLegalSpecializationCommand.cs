using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.AdminLegalSpecializations.Common;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.AdminLegalSpecializations.UpdateLegalSpecialization;

public sealed record UpdateLegalSpecializationCommand(
    int Id,
    string NameAr,
    string NameEn,
    int DisplayOrder,
    string RowVersion)
    : ICommand<AdminLegalSpecializationResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class UpdateLegalSpecializationCommandValidator
    : AbstractValidator<UpdateLegalSpecializationCommand>
{
    public UpdateLegalSpecializationCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(150);
        RuleFor(command => command.NameEn).NotEmpty().MaximumLength(150);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.RowVersion)
            .Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("LegalSpecialization.InvalidRowVersion");
    }
}

internal sealed class UpdateLegalSpecializationCommandHandler(
    IReadRepository<LegalSpecialization, LawyerPlatformReadPersistence> reader,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager)
    : ICommandHandler<UpdateLegalSpecializationCommand, AdminLegalSpecializationResponse>
{
    public async Task<Result<AdminLegalSpecializationResponse>> Handle(
        UpdateLegalSpecializationCommand command,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.WriteRepository<LegalSpecialization>();
        var item = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (item is null)
        {
            return Result<AdminLegalSpecializationResponse>.Fail(LegalSpecializationErrors.NotFound);
        }

        var conflict = await LegalSpecializationNameConflictChecker.FindConflictAsync(
            reader,
            command.NameAr,
            command.NameEn,
            command.Id,
            cancellationToken);
        if (conflict is not null)
        {
            return Result<AdminLegalSpecializationResponse>.Fail(conflict);
        }

        var rowVersion = RowVersionCodec.Decode(command.RowVersion);
        if (rowVersion.IsFailure)
        {
            return Result<AdminLegalSpecializationResponse>.Fail(LegalSpecializationErrors.InvalidRowVersion);
        }

        concurrencyTokenManager.SetOriginalRowVersion(item, rowVersion.Value);
        var update = item.Update(command.NameAr, command.NameEn, command.DisplayOrder);
        if (update.IsFailure)
        {
            return Result<AdminLegalSpecializationResponse>.Fail(update.Errors);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AdminLegalSpecializationResponse>.Ok(new AdminLegalSpecializationResponse(
            item.Id,
            item.NameAr,
            item.NameEn,
            item.DisplayOrder,
            item.IsActive,
            RowVersionCodec.Encode(item.RowVersion)));
    }
}
