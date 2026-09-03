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

namespace LawyerPlatform.Application.Features.AdminLocations.Areas;

public sealed record GetAdminAreasQuery(
    int? GovernorateId, int? CityId, string? SearchText, bool? IsActive,
    int PageNumber = 1, int PageSize = 20) : IQuery<PagedResult<AdminAreaResponse>>;
public sealed record GetAdminAreaQuery(int Id) : IQuery<AdminAreaResponse>;
public sealed record CreateAreaCommand(int CityId, string NameAr, string NameEn, int DisplayOrder)
    : ICommand<AdminAreaResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;
public sealed record UpdateAreaCommand(
    int Id, int CityId, string NameAr, string NameEn, int DisplayOrder, string RowVersion)
    : ICommand<AdminAreaResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;
public sealed record ChangeAreaStatusCommand(int Id, bool Activate, string RowVersion)
    : ICommand<AdminAreaResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class GetAdminAreasQueryValidator : AbstractValidator<GetAdminAreasQuery>
{
    public GetAdminAreasQueryValidator()
    {
        RuleFor(query => query.GovernorateId).GreaterThan(0).When(query => query.GovernorateId.HasValue);
        RuleFor(query => query.CityId).GreaterThan(0).When(query => query.CityId.HasValue);
        RuleFor(query => query.SearchText).MaximumLength(Area.MaximumNameLength);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class CreateAreaCommandValidator : AbstractValidator<CreateAreaCommand>
{
    public CreateAreaCommandValidator()
    {
        RuleFor(command => command.CityId).GreaterThan(0);
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(Area.MaximumNameLength);
        RuleFor(command => command.NameEn).NotEmpty().MaximumLength(Area.MaximumNameLength);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateAreaCommandValidator : AbstractValidator<UpdateAreaCommand>
{
    public UpdateAreaCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);
        RuleFor(command => command.CityId).GreaterThan(0);
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(Area.MaximumNameLength);
        RuleFor(command => command.NameEn).NotEmpty().MaximumLength(Area.MaximumNameLength);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.RowVersion).Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Area.InvalidRowVersion");
    }
}

internal sealed class ChangeAreaStatusCommandValidator : AbstractValidator<ChangeAreaStatusCommand>
{
    public ChangeAreaStatusCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);
        RuleFor(command => command.RowVersion).Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Area.InvalidRowVersion");
    }
}

internal sealed class GetAdminAreasQueryHandler(
    IReadRepository<Area, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetAdminAreasQuery, PagedResult<AdminAreaResponse>>
{
    public async Task<Result<PagedResult<AdminAreaResponse>>> Handle(
        GetAdminAreasQuery query, CancellationToken cancellationToken)
    {
        var (items, count) = await repository.ListWithLongCountAsync(
            new AdminAreasSpecification(query), cancellationToken);
        return Result<PagedResult<AdminAreaResponse>>.Ok(new(
            query.PageNumber, query.PageSize, count, items.Select(item => item.ToResponse())));
    }
}

internal sealed class GetAdminAreaQueryHandler(
    IReadRepository<Area, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetAdminAreaQuery, AdminAreaResponse>
{
    public async Task<Result<AdminAreaResponse>> Handle(GetAdminAreaQuery query, CancellationToken cancellationToken)
    {
        var item = await repository.FirstOrDefaultAsync(new AreaByIdSpecification(query.Id), cancellationToken);
        return item is null ? Result<AdminAreaResponse>.Fail(AreaErrors.NotFound) : Result<AdminAreaResponse>.Ok(item.ToResponse());
    }
}

internal sealed class CreateAreaCommandHandler(
    IReadRepository<City, LawyerPlatformReadPersistence> cities,
    IReadRepository<Area, LawyerPlatformReadPersistence> areas,
    IReferenceDataIdGenerator idGenerator,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork)
    : ICommandHandler<CreateAreaCommand, AdminAreaResponse>
{
    public async Task<Result<AdminAreaResponse>> Handle(CreateAreaCommand command, CancellationToken cancellationToken)
    {
        var parent = await cities.FirstOrDefaultAsync(new CityParentByIdSpecification(command.CityId), cancellationToken);
        if (parent is null) return Result<AdminAreaResponse>.Fail(AreaErrors.InvalidCity);
        var conflict = await LocationConflictChecker.FindAreaConflictAsync(
            areas, command.CityId, command.NameAr, command.NameEn, null, cancellationToken);
        if (conflict is not null) return Result<AdminAreaResponse>.Fail(conflict);
        var id = await idGenerator.NextAreaIdAsync(cancellationToken);
        var creation = Area.Create(id,
            command.CityId, command.NameAr, command.NameEn, command.DisplayOrder);
        if (creation.IsFailure) return Result<AdminAreaResponse>.Fail(creation.Errors);
        var item = creation.Value;
        await unitOfWork.WriteRepository<Area>().AddAsync(item, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AdminAreaResponse>.Ok(AreaMapper.ToResponse(item, parent));
    }
}

internal sealed class UpdateAreaCommandHandler(
    IReadRepository<City, LawyerPlatformReadPersistence> cities,
    IReadRepository<Area, LawyerPlatformReadPersistence> areas,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager)
    : ICommandHandler<UpdateAreaCommand, AdminAreaResponse>
{
    public async Task<Result<AdminAreaResponse>> Handle(UpdateAreaCommand command, CancellationToken cancellationToken)
    {
        var item = await unitOfWork.WriteRepository<Area>().GetByIdAsync(command.Id, cancellationToken);
        if (item is null) return Result<AdminAreaResponse>.Fail(AreaErrors.NotFound);
        var parent = await cities.FirstOrDefaultAsync(new CityParentByIdSpecification(command.CityId), cancellationToken);
        if (parent is null) return Result<AdminAreaResponse>.Fail(AreaErrors.InvalidCity);
        var conflict = await LocationConflictChecker.FindAreaConflictAsync(
            areas, command.CityId, command.NameAr, command.NameEn, command.Id, cancellationToken);
        if (conflict is not null) return Result<AdminAreaResponse>.Fail(conflict);
        var rowVersion = RowVersionCodec.Decode(command.RowVersion);
        if (rowVersion.IsFailure) return Result<AdminAreaResponse>.Fail(AreaErrors.InvalidRowVersion);
        concurrencyTokenManager.SetOriginalRowVersion(item, rowVersion.Value);
        var update = item.Update(command.CityId, command.NameAr, command.NameEn, command.DisplayOrder);
        if (update.IsFailure) return Result<AdminAreaResponse>.Fail(update.Errors);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AdminAreaResponse>.Ok(AreaMapper.ToResponse(item, parent));
    }
}

internal sealed class ChangeAreaStatusCommandHandler(
    IReadRepository<City, LawyerPlatformReadPersistence> cities,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager)
    : ICommandHandler<ChangeAreaStatusCommand, AdminAreaResponse>
{
    public async Task<Result<AdminAreaResponse>> Handle(ChangeAreaStatusCommand command, CancellationToken cancellationToken)
    {
        var item = await unitOfWork.WriteRepository<Area>().GetByIdAsync(command.Id, cancellationToken);
        if (item is null) return Result<AdminAreaResponse>.Fail(AreaErrors.NotFound);
        var parent = await cities.FirstOrDefaultAsync(new CityParentByIdSpecification(item.CityId), cancellationToken);
        var rowVersion = RowVersionCodec.Decode(command.RowVersion);
        if (rowVersion.IsFailure) return Result<AdminAreaResponse>.Fail(AreaErrors.InvalidRowVersion);
        concurrencyTokenManager.SetOriginalRowVersion(item, rowVersion.Value);
        var change = command.Activate ? item.Activate() : item.Deactivate();
        if (change.IsFailure) return Result<AdminAreaResponse>.Fail(change.Errors);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AdminAreaResponse>.Ok(AreaMapper.ToResponse(item, parent!));
    }
}

internal sealed class AdminAreasSpecification : Specification<Area, AreaSnapshot>
{
    public AdminAreasSpecification(GetAdminAreasQuery query)
    {
        if (query.GovernorateId.HasValue) AddCriteria(item => item.City.GovernorateId == query.GovernorateId.Value);
        if (query.CityId.HasValue) AddCriteria(item => item.CityId == query.CityId.Value);
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
        Select(item => new AreaSnapshot(
            item.Id, item.City.GovernorateId, item.City.Governorate.NameAr, item.City.Governorate.NameEn,
            item.CityId, item.City.NameAr, item.City.NameEn,
            item.NameAr, item.NameEn, item.DisplayOrder, item.IsActive, item.RowVersion));
    }
}

file static class AreaMapper
{
    public static AdminAreaResponse ToResponse(Area item, CityParentSnapshot parent) => new(
        item.Id, parent.GovernorateId, parent.GovernorateNameAr, parent.GovernorateNameEn,
        item.CityId, parent.NameAr, parent.NameEn,
        item.NameAr, item.NameEn, item.DisplayOrder, item.IsActive,
        RowVersionCodec.Encode(item.RowVersion));
}
