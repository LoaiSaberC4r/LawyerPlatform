using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.SharedDto;
using BuildingBlock.Domain.Specification;
using FluentValidation;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Abstractions.ReferenceData;
using LawyerPlatform.Application.Features.AdminLocations.Common;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.AdminLocations.Governorates;

public sealed record GetAdminGovernoratesQuery(
    string? SearchText, bool? IsActive, int PageNumber = 1, int PageSize = 20)
    : IQuery<PagedResult<AdminGovernorateResponse>>;
public sealed record GetAdminGovernorateQuery(int Id) : IQuery<AdminGovernorateResponse>;
public sealed record CreateGovernorateCommand(string NameAr, string NameEn, int DisplayOrder)
    : ICommand<AdminGovernorateResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;
public sealed record UpdateGovernorateCommand(
    int Id, string NameAr, string NameEn, int DisplayOrder, string RowVersion)
    : ICommand<AdminGovernorateResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;
public sealed record ChangeGovernorateStatusCommand(int Id, bool Activate, string RowVersion)
    : ICommand<AdminGovernorateResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class GetAdminGovernoratesQueryValidator : AbstractValidator<GetAdminGovernoratesQuery>
{
    public GetAdminGovernoratesQueryValidator()
    {
        RuleFor(query => query.SearchText).MaximumLength(Governorate.MaximumNameLength);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class CreateGovernorateCommandValidator : AbstractValidator<CreateGovernorateCommand>
{
    public CreateGovernorateCommandValidator()
    {
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(Governorate.MaximumNameLength);
        RuleFor(command => command.NameEn).NotEmpty().MaximumLength(Governorate.MaximumNameLength);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateGovernorateCommandValidator : AbstractValidator<UpdateGovernorateCommand>
{
    public UpdateGovernorateCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(Governorate.MaximumNameLength);
        RuleFor(command => command.NameEn).NotEmpty().MaximumLength(Governorate.MaximumNameLength);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.RowVersion).Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Governorate.InvalidRowVersion");
    }
}

internal sealed class ChangeGovernorateStatusCommandValidator : AbstractValidator<ChangeGovernorateStatusCommand>
{
    public ChangeGovernorateStatusCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);
        RuleFor(command => command.RowVersion).Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Governorate.InvalidRowVersion");
    }
}

internal sealed class GetAdminGovernoratesQueryHandler(
    IReadRepository<Governorate, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetAdminGovernoratesQuery, PagedResult<AdminGovernorateResponse>>
{
    public async Task<Result<PagedResult<AdminGovernorateResponse>>> Handle(
        GetAdminGovernoratesQuery query, CancellationToken cancellationToken)
    {
        var (items, count) = await repository.ListWithLongCountAsync(
            new AdminGovernoratesSpecification(query), cancellationToken);
        return Result<PagedResult<AdminGovernorateResponse>>.Ok(new(
            query.PageNumber, query.PageSize, count, items.Select(item => item.ToResponse())));
    }
}

internal sealed class GetAdminGovernorateQueryHandler(
    IReadRepository<Governorate, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetAdminGovernorateQuery, AdminGovernorateResponse>
{
    public async Task<Result<AdminGovernorateResponse>> Handle(
        GetAdminGovernorateQuery query, CancellationToken cancellationToken)
    {
        var item = await repository.FirstOrDefaultAsync(new GovernorateByIdSpecification(query.Id), cancellationToken);
        return item is null
            ? Result<AdminGovernorateResponse>.Fail(GovernorateErrors.NotFound)
            : Result<AdminGovernorateResponse>.Ok(item.ToResponse());
    }
}

internal sealed class CreateGovernorateCommandHandler(
    IReadRepository<Governorate, LawyerPlatformReadPersistence> reader,
    IReferenceDataIdGenerator idGenerator,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork)
    : ICommandHandler<CreateGovernorateCommand, AdminGovernorateResponse>
{
    public async Task<Result<AdminGovernorateResponse>> Handle(
        CreateGovernorateCommand command, CancellationToken cancellationToken)
    {
        var conflict = await LocationConflictChecker.FindGovernorateConflictAsync(
            reader, command.NameAr, command.NameEn, null, cancellationToken);
        if (conflict is not null) return Result<AdminGovernorateResponse>.Fail(conflict);
        var id = await idGenerator.NextGovernorateIdAsync(cancellationToken);
        var creation = Governorate.Create(id,
            command.NameAr, command.NameEn, command.DisplayOrder);
        if (creation.IsFailure) return Result<AdminGovernorateResponse>.Fail(creation.Errors);
        var item = creation.Value;
        await unitOfWork.WriteRepository<Governorate>().AddAsync(item, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AdminGovernorateResponse>.Ok(GovernorateMapper.ToResponse(item));
    }
}

internal sealed class UpdateGovernorateCommandHandler(
    IReadRepository<Governorate, LawyerPlatformReadPersistence> reader,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager)
    : ICommandHandler<UpdateGovernorateCommand, AdminGovernorateResponse>
{
    public async Task<Result<AdminGovernorateResponse>> Handle(
        UpdateGovernorateCommand command, CancellationToken cancellationToken)
    {
        var item = await unitOfWork.WriteRepository<Governorate>().GetByIdAsync(command.Id, cancellationToken);
        if (item is null) return Result<AdminGovernorateResponse>.Fail(GovernorateErrors.NotFound);
        var conflict = await LocationConflictChecker.FindGovernorateConflictAsync(
            reader, command.NameAr, command.NameEn, command.Id, cancellationToken);
        if (conflict is not null) return Result<AdminGovernorateResponse>.Fail(conflict);
        var rowVersion = RowVersionCodec.Decode(command.RowVersion);
        if (rowVersion.IsFailure) return Result<AdminGovernorateResponse>.Fail(GovernorateErrors.InvalidRowVersion);
        concurrencyTokenManager.SetOriginalRowVersion(item, rowVersion.Value);
        var update = item.Update(command.NameAr, command.NameEn, command.DisplayOrder);
        if (update.IsFailure) return Result<AdminGovernorateResponse>.Fail(update.Errors);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AdminGovernorateResponse>.Ok(GovernorateMapper.ToResponse(item));
    }
}

internal sealed class ChangeGovernorateStatusCommandHandler(
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager)
    : ICommandHandler<ChangeGovernorateStatusCommand, AdminGovernorateResponse>
{
    public async Task<Result<AdminGovernorateResponse>> Handle(
        ChangeGovernorateStatusCommand command, CancellationToken cancellationToken)
    {
        var item = await unitOfWork.WriteRepository<Governorate>().GetByIdAsync(command.Id, cancellationToken);
        if (item is null) return Result<AdminGovernorateResponse>.Fail(GovernorateErrors.NotFound);
        var rowVersion = RowVersionCodec.Decode(command.RowVersion);
        if (rowVersion.IsFailure) return Result<AdminGovernorateResponse>.Fail(GovernorateErrors.InvalidRowVersion);
        concurrencyTokenManager.SetOriginalRowVersion(item, rowVersion.Value);
        var change = command.Activate ? item.Activate() : item.Deactivate();
        if (change.IsFailure) return Result<AdminGovernorateResponse>.Fail(change.Errors);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AdminGovernorateResponse>.Ok(GovernorateMapper.ToResponse(item));
    }
}

internal sealed class AdminGovernoratesSpecification : Specification<Governorate, GovernorateSnapshot>
{
    public AdminGovernoratesSpecification(GetAdminGovernoratesQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var search = query.SearchText.Trim();
            AddCriteria(item => item.NameAr.Contains(search) || item.NameEn.Contains(search));
        }
        if (query.IsActive.HasValue) AddCriteria(item => item.IsActive == query.IsActive.Value);
        AddOrderBy(item => item.DisplayOrder);
        AddOrderBy(item => item.Id);
        ApplyPaging(query.PageNumber, query.PageSize, 100);
        UseNoTracking();
        Select(item => new GovernorateSnapshot(
            item.Id, item.NameAr, item.NameEn, item.DisplayOrder, item.IsActive, item.RowVersion));
    }
}

file static class GovernorateMapper
{
    public static AdminGovernorateResponse ToResponse(Governorate item) => new(
        item.Id, item.NameAr, item.NameEn, item.DisplayOrder, item.IsActive,
        RowVersionCodec.Encode(item.RowVersion));
}
