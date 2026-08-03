using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.DeleteDocument;

public sealed record DeleteDocumentCommand(Guid DocumentId, string RowVersion)
    : ICommand, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class DeleteDocumentCommandValidator : AbstractValidator<DeleteDocumentCommand>
{
    public DeleteDocumentCommandValidator()
    {
        RuleFor(command => command.DocumentId).NotEmpty();
        RuleFor(command => command.RowVersion)
            .Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Lawyer.InvalidRowVersion");
    }
}

internal sealed class DeleteDocumentCommandHandler(
    ICurrentUser currentUser,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager,
    ILawyerDocumentPolicy documentPolicy,
    IDateTimeProvider clock)
    : ICommandHandler<DeleteDocumentCommand>
{
    public async Task<Result> Handle(DeleteDocumentCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Fail(LawyerApplicationErrors.AccountNotFound);
        }

        var profile = await unitOfWork.WriteRepository<LawyerProfile>()
            .FirstOrDefaultAsync(new LawyerAggregateByUserAccountIdSpecification(userId), cancellationToken);
        if (profile is null)
        {
            return Result.Fail(LawyerErrors.DocumentNotFound);
        }

        var document = profile.Documents.SingleOrDefault(item => item.Id == command.DocumentId && !item.IsDeleted);
        if (document is null)
        {
            return Result.Fail(LawyerErrors.DocumentNotFound);
        }

        var rowVersion = RowVersionCodec.Decode(command.RowVersion);
        if (rowVersion.IsFailure)
        {
            return Result.Fail(rowVersion.Errors);
        }

        concurrencyTokenManager.SetOriginalRowVersion(document, rowVersion.Value);
        var remove = profile.RemoveDocument(command.DocumentId, documentPolicy.RequiredDocumentTypes, clock.UtcNow);
        if (remove.IsFailure)
        {
            return remove;
        }

        profile.ModifiedOnUtc = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
