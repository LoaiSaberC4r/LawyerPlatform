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

namespace LawyerPlatform.Application.Features.AdminLegalSpecializations.ChangeLegalSpecializationStatus;

public sealed record ChangeLegalSpecializationStatusCommand(int Id, bool Activate, string RowVersion)
    : ICommand<AdminLegalSpecializationResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class ChangeLegalSpecializationStatusCommandValidator
    : AbstractValidator<ChangeLegalSpecializationStatusCommand>
{
    public ChangeLegalSpecializationStatusCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);
        RuleFor(command => command.RowVersion)
            .Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("LegalSpecialization.InvalidRowVersion");
    }
}

internal sealed class ChangeLegalSpecializationStatusCommandHandler(
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager)
    : ICommandHandler<ChangeLegalSpecializationStatusCommand, AdminLegalSpecializationResponse>
{
    public async Task<Result<AdminLegalSpecializationResponse>> Handle(
        ChangeLegalSpecializationStatusCommand command,
        CancellationToken cancellationToken)
    {
        var item = await unitOfWork.WriteRepository<LegalSpecialization>()
            .GetByIdAsync(command.Id, cancellationToken);
        if (item is null)
        {
            return Result<AdminLegalSpecializationResponse>.Fail(LegalSpecializationErrors.NotFound);
        }

        var rowVersion = RowVersionCodec.Decode(command.RowVersion);
        if (rowVersion.IsFailure)
        {
            return Result<AdminLegalSpecializationResponse>.Fail(LegalSpecializationErrors.InvalidRowVersion);
        }

        concurrencyTokenManager.SetOriginalRowVersion(item, rowVersion.Value);
        var change = command.Activate ? item.Activate() : item.Deactivate();
        if (change.IsFailure)
        {
            return Result<AdminLegalSpecializationResponse>.Fail(change.Errors);
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
