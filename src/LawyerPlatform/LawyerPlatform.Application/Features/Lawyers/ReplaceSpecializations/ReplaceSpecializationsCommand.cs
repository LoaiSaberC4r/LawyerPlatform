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
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.Lawyers.ReplaceSpecializations;

public sealed record ReplaceSpecializationsCommand(IReadOnlyList<int> SpecializationIds, string RowVersion)
    : ICommand<ReplaceSpecializationsResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

public sealed record ReplaceSpecializationsResponse(
    IReadOnlyList<LawyerSpecializationResponse> Specializations,
    string RowVersion);

internal sealed class ReplaceSpecializationsCommandValidator : AbstractValidator<ReplaceSpecializationsCommand>
{
    public ReplaceSpecializationsCommandValidator()
    {
        RuleFor(command => command.SpecializationIds).NotNull();
        RuleFor(command => command.SpecializationIds)
            .Must(ids => ids is not null && ids.Count == ids.Distinct().Count())
            .WithErrorCode("Lawyer.DuplicateSpecialization");
        RuleForEach(command => command.SpecializationIds).GreaterThan(0);
        RuleFor(command => command.RowVersion)
            .Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Lawyer.InvalidRowVersion");
    }
}

internal sealed class ReplaceSpecializationsCommandHandler(
    ICurrentUser currentUser,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IReadRepository<LegalSpecialization, LawyerPlatformReadPersistence> specializationReader,
    IConcurrencyTokenManager concurrencyTokenManager,
    ILawyerAggregatePersistence aggregatePersistence,
    IDateTimeProvider clock)
    : ICommandHandler<ReplaceSpecializationsCommand, ReplaceSpecializationsResponse>
{
    public async Task<Result<ReplaceSpecializationsResponse>> Handle(ReplaceSpecializationsCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<ReplaceSpecializationsResponse>.Fail(LawyerApplicationErrors.AccountNotFound);
        }

        if (command.SpecializationIds.Count != command.SpecializationIds.Distinct().Count())
        {
            return Result<ReplaceSpecializationsResponse>.Fail(LawyerErrors.DuplicateSpecialization);
        }

        var requested = command.SpecializationIds.Count == 0
            ? []
            : await specializationReader.ListAsync(new RequestedSpecializationsSpecification(command.SpecializationIds), cancellationToken);
        if (requested.Count != command.SpecializationIds.Count)
        {
            return Result<ReplaceSpecializationsResponse>.Fail(LawyerApplicationErrors.SpecializationNotFound);
        }

        if (requested.Any(item => !item.IsActive))
        {
            return Result<ReplaceSpecializationsResponse>.Fail(LawyerApplicationErrors.SpecializationInactive);
        }

        var profile = await unitOfWork.WriteRepository<LawyerProfile>()
            .FirstOrDefaultAsync(new LawyerAggregateByUserAccountIdSpecification(userId), cancellationToken);
        if (profile is null)
        {
            return Result<ReplaceSpecializationsResponse>.Fail(LawyerErrors.NotFound);
        }

        var rowVersion = RowVersionCodec.Decode(command.RowVersion);
        if (rowVersion.IsFailure)
        {
            return Result<ReplaceSpecializationsResponse>.Fail(rowVersion.Errors);
        }

        concurrencyTokenManager.SetOriginalRowVersion(profile, rowVersion.Value);
        var previousSpecializations = profile.Specializations.ToArray();
        var replace = profile.ReplaceSpecializations(command.SpecializationIds);
        if (replace.IsFailure)
        {
            return Result<ReplaceSpecializationsResponse>.Fail(replace.Errors);
        }

        aggregatePersistence.RemoveRange(previousSpecializations);
        aggregatePersistence.AddRange(profile.Specializations);

        profile.ModifiedOnUtc = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var responses = await specializationReader.ListAsync(
            new SelectedSpecializationsSpecification(command.SpecializationIds),
            cancellationToken);
        return Result<ReplaceSpecializationsResponse>.Ok(new ReplaceSpecializationsResponse(
            responses,
            RowVersionCodec.Encode(profile.RowVersion)));
    }
}

internal sealed class SelectedSpecializationsSpecification : BuildingBlock.Domain.Specification.Specification<LegalSpecialization, LawyerSpecializationResponse>
{
    public SelectedSpecializationsSpecification(IReadOnlyCollection<int> ids)
    {
        AddCriteria(item => ids.Contains(item.Id));
        AddOrderBy(item => item.DisplayOrder);
        AddOrderBy(item => item.Id);
        UseNoTracking();
        Select(item => new LawyerSpecializationResponse(item.Id, item.NameAr, item.NameEn));
    }
}
