using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Features.AdminLegalSpecializations.Common;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Abstractions.ReferenceData;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.AdminLegalSpecializations.CreateLegalSpecialization;

public sealed record CreateLegalSpecializationCommand(string NameAr, string NameEn, int DisplayOrder)
    : ICommand<AdminLegalSpecializationResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class CreateLegalSpecializationCommandValidator
    : AbstractValidator<CreateLegalSpecializationCommand>
{
    public CreateLegalSpecializationCommandValidator()
    {
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(150);
        RuleFor(command => command.NameEn).NotEmpty().MaximumLength(150);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class CreateLegalSpecializationCommandHandler(
    IReadRepository<LegalSpecialization, LawyerPlatformReadPersistence> reader,
    IReferenceDataIdGenerator idGenerator,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork)
    : ICommandHandler<CreateLegalSpecializationCommand, AdminLegalSpecializationResponse>
{
    public async Task<Result<AdminLegalSpecializationResponse>> Handle(
        CreateLegalSpecializationCommand command,
        CancellationToken cancellationToken)
    {
        var conflict = await LegalSpecializationNameConflictChecker.FindConflictAsync(
            reader,
            command.NameAr,
            command.NameEn,
            null,
            cancellationToken);
        if (conflict is not null)
        {
            return Result<AdminLegalSpecializationResponse>.Fail(conflict);
        }

        var id = await idGenerator.NextLegalSpecializationIdAsync(cancellationToken);
        var creation = LegalSpecialization.Create(id, command.NameAr, command.NameEn, command.DisplayOrder);
        if (creation.IsFailure)
        {
            return Result<AdminLegalSpecializationResponse>.Fail(creation.Errors);
        }

        var item = creation.Value;
        await unitOfWork.WriteRepository<LegalSpecialization>().AddAsync(item, cancellationToken);
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
