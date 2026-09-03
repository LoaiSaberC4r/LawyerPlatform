using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Abstractions.Media;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;
using Microsoft.Extensions.Logging;

namespace LawyerPlatform.Application.Features.Lawyers.UpdateProfileImage;

public sealed record UpdateProfileImageCommand(MediaUpload Image, string RowVersion)
    : ICommand<UpdateProfileImageResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

public sealed record UpdateProfileImageResponse(bool HasProfileImage, string? ProfileImagePath, string RowVersion);

internal sealed class UpdateProfileImageCommandValidator : AbstractValidator<UpdateProfileImageCommand>
{
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".gif"];

    public UpdateProfileImageCommandValidator()
    {
        RuleFor(command => command.Image).NotNull().WithErrorCode("Lawyer.InvalidDocument");
        RuleFor(command => command.Image.FileName)
            .Must(fileName => Extensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase))
            .When(command => command.Image is not null)
            .WithErrorCode("Lawyer.InvalidDocument");
        RuleFor(command => command.RowVersion)
            .Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Lawyer.InvalidRowVersion");
    }
}

internal sealed class UpdateProfileImageCommandHandler(
    ICurrentUser currentUser,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager,
    IMediaService mediaService,
    IProfileImagePathResolver profileImagePathResolver,
    IDateTimeProvider clock,
    ILogger<UpdateProfileImageCommandHandler> logger)
    : ICommandHandler<UpdateProfileImageCommand, UpdateProfileImageResponse>
{
    private static readonly Action<ILogger, string, Exception?> CleanupFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(4100, nameof(CleanupFailed)),
        "Lawyer profile image cleanup failed. ExceptionType={ExceptionType}");

    public async Task<Result<UpdateProfileImageResponse>> Handle(UpdateProfileImageCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<UpdateProfileImageResponse>.Fail(LawyerApplicationErrors.AccountNotFound);
        }

        var profile = await unitOfWork.WriteRepository<LawyerProfile>()
            .FirstOrDefaultAsync(new LawyerAggregateByUserAccountIdSpecification(userId), cancellationToken);
        if (profile is null)
        {
            return Result<UpdateProfileImageResponse>.Fail(LawyerErrors.NotFound);
        }

        var rowVersion = RowVersionCodec.Decode(command.RowVersion);
        if (rowVersion.IsFailure)
        {
            return Result<UpdateProfileImageResponse>.Fail(rowVersion.Errors);
        }

        concurrencyTokenManager.SetOriginalRowVersion(profile, rowVersion.Value);
        var oldStorageKey = profile.ProfileImageStorageKey;
        var stored = await mediaService.SaveAsync(
            command.Image,
            new MediaStorageRequest($"lawyers/{profile.Id:N}/profile"),
            cancellationToken);

        var replace = profile.ReplaceProfileImage(stored.Key);
        if (replace.IsFailure)
        {
            await TryDeleteAsync(stored.Key, cancellationToken);
            return Result<UpdateProfileImageResponse>.Fail(replace.Errors);
        }

        profile.ModifiedOnUtc = clock.UtcNow;
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteAsync(stored.Key, CancellationToken.None);
            throw;
        }

        if (oldStorageKey is not null)
        {
            await TryDeleteAsync(oldStorageKey, CancellationToken.None);
        }

        return Result<UpdateProfileImageResponse>.Ok(new UpdateProfileImageResponse(
            true,
            profileImagePathResolver.Resolve(stored.Key),
            RowVersionCodec.Encode(profile.RowVersion)));
    }

    private async Task TryDeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await mediaService.DeleteAsync(key, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CleanupFailed(logger, exception.GetType().Name, null);
        }
    }
}
