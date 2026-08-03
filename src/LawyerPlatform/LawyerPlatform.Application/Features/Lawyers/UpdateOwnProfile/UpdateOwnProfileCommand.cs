using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Features.Lawyers.GetOwnProfile;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.UpdateOwnProfile;

public sealed record UpdateOwnProfileCommand(
    string FullName,
    string? ProfessionalTitle,
    string? Biography,
    int? YearsOfExperience,
    string? ProfessionalRegistrationNumber,
    string RowVersion)
    : ICommand<LawyerOwnProfileResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class UpdateOwnProfileCommandValidator : AbstractValidator<UpdateOwnProfileCommand>
{
    public UpdateOwnProfileCommandValidator()
    {
        RuleFor(command => command.FullName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.ProfessionalTitle).MaximumLength(200);
        RuleFor(command => command.Biography).MaximumLength(4000);
        RuleFor(command => command.YearsOfExperience).GreaterThanOrEqualTo(0).When(command => command.YearsOfExperience.HasValue);
        RuleFor(command => command.ProfessionalRegistrationNumber).MaximumLength(100);
        RuleFor(command => command.RowVersion)
            .Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Lawyer.InvalidRowVersion");
    }
}

internal sealed class UpdateOwnProfileCommandHandler(
    ICurrentUser currentUser,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> reader,
    IConcurrencyTokenManager concurrencyTokenManager,
    ILawyerDocumentPolicy documentPolicy,
    IDateTimeProvider clock)
    : ICommandHandler<UpdateOwnProfileCommand, LawyerOwnProfileResponse>
{
    public async Task<Result<LawyerOwnProfileResponse>> Handle(UpdateOwnProfileCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<LawyerOwnProfileResponse>.Fail(LawyerApplicationErrors.AccountNotFound);
        }

        var repository = unitOfWork.WriteRepository<LawyerProfile>();
        var profile = await repository.FirstOrDefaultAsync(new LawyerAggregateByUserAccountIdSpecification(userId), cancellationToken);
        if (profile is null)
        {
            return Result<LawyerOwnProfileResponse>.Fail(LawyerErrors.NotFound);
        }

        var rowVersion = RowVersionCodec.Decode(command.RowVersion);
        if (rowVersion.IsFailure)
        {
            return Result<LawyerOwnProfileResponse>.Fail(rowVersion.Errors);
        }

        var registrationNumber = string.IsNullOrWhiteSpace(command.ProfessionalRegistrationNumber)
            ? null
            : command.ProfessionalRegistrationNumber.Trim();
        if (registrationNumber is not null &&
            await reader.AnyAsync(item => item.Id != profile.Id && item.ProfessionalRegistrationNumber == registrationNumber, cancellationToken))
        {
            return Result<LawyerOwnProfileResponse>.Fail(LawyerErrors.RegistrationNumberAlreadyExists);
        }

        concurrencyTokenManager.SetOriginalRowVersion(profile, rowVersion.Value);
        var update = profile.UpdateProfessionalProfile(
            command.FullName,
            command.ProfessionalTitle,
            command.Biography,
            command.YearsOfExperience,
            registrationNumber);
        if (update.IsFailure)
        {
            return Result<LawyerOwnProfileResponse>.Fail(update.Errors);
        }

        profile.ModifiedOnUtc = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var snapshot = await reader.FirstOrDefaultAsync(new OwnProfileSpecification(userId), cancellationToken);
        return snapshot is null
            ? Result<LawyerOwnProfileResponse>.Fail(LawyerErrors.NotFound)
            : Result<LawyerOwnProfileResponse>.Ok(OwnProfileMapper.Map(snapshot, documentPolicy));
    }
}
