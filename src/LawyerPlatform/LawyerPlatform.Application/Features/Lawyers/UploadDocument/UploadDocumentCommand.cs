using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Features.Lawyers.GetOwnDocuments;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;
using Microsoft.Extensions.Logging;

namespace LawyerPlatform.Application.Features.Lawyers.UploadDocument;

public sealed record UploadDocumentCommand(string DocumentType, MediaUpload File)
    : ICommand<LawyerDocumentResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(command => command.DocumentType)
            .NotEmpty().WithErrorCode("Lawyer.DocumentTypeRequired")
            .MaximumLength(100);
        RuleFor(command => command.File).NotNull().WithErrorCode("Lawyer.InvalidDocument");
    }
}

internal sealed class UploadDocumentCommandHandler(
    ICurrentUser currentUser,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    ILawyerDocumentPolicy documentPolicy,
    IMediaService mediaService,
    IDateTimeProvider clock,
    ILawyerAggregatePersistence aggregatePersistence,
    ILogger<UploadDocumentCommandHandler> logger)
    : ICommandHandler<UploadDocumentCommand, LawyerDocumentResponse>
{
    private static readonly Action<ILogger, string, Exception?> CompensationFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(4101, nameof(CompensationFailed)),
        "Lawyer document storage compensation failed. ExceptionType={ExceptionType}");

    public async Task<Result<LawyerDocumentResponse>> Handle(UploadDocumentCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<LawyerDocumentResponse>.Fail(LawyerApplicationErrors.AccountNotFound);
        }

        var validation = await documentPolicy.ValidateAsync(command.DocumentType, command.File, cancellationToken);
        if (validation.IsFailure)
        {
            return Result<LawyerDocumentResponse>.Fail(validation.Errors);
        }

        var profile = await unitOfWork.WriteRepository<LawyerProfile>()
            .FirstOrDefaultAsync(new LawyerAggregateByUserAccountIdSpecification(userId), cancellationToken);
        if (profile is null)
        {
            return Result<LawyerDocumentResponse>.Fail(LawyerErrors.NotFound);
        }

        var stored = await mediaService.SaveAsync(
            command.File,
            new MediaStorageRequest($"lawyers/{profile.Id:N}/documents"),
            cancellationToken);
        var added = profile.AddDocument(
            command.DocumentType,
            stored.Key,
            Path.GetFileName(command.File.FileName),
            stored.ContentType,
            stored.Length,
            clock.UtcNow);
        if (added.IsFailure)
        {
            await TryCompensateAsync(stored.Key, cancellationToken);
            return Result<LawyerDocumentResponse>.Fail(added.Errors);
        }

        aggregatePersistence.Add(added.Value);

        profile.ModifiedOnUtc = clock.UtcNow;
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryCompensateAsync(stored.Key, CancellationToken.None);
            throw;
        }

        var document = added.Value;
        return Result<LawyerDocumentResponse>.Ok(DocumentMapper.Map(new DocumentMetadataSnapshot(
            document.Id,
            document.DocumentType,
            document.OriginalFileName,
            document.ContentType,
            document.FileSize,
            document.UploadedOnUtc,
            document.RowVersion)));
    }

    private async Task TryCompensateAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            await mediaService.DeleteAsync(storageKey, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CompensationFailed(logger, exception.GetType().Name, null);
        }
    }
}
