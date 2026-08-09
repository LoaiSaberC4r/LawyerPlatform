using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.SharedDto;
using BuildingBlock.Domain.Specification;
using FluentValidation;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.AdminLocations.Common;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.AdminLocations.Cities;

public sealed record GetAdminCitiesQuery(
    int? GovernorateId, string? SearchText, bool? IsActive, int PageNumber = 1, int PageSize = 20)
    : IQuery<PagedResult<AdminCityResponse>>;
public sealed record GetAdminCityQuery(int Id) : IQuery<AdminCityResponse>;
public sealed record CreateCityCommand(
    int GovernorateId, string NameAr, string NameEn, int DisplayOrder)
    : ICommand<AdminCityResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;
public sealed record UpdateCityCommand(
    int Id, int GovernorateId, string NameAr, string NameEn, int DisplayOrder, string RowVersion)
    : ICommand<AdminCityResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;
public sealed record ChangeCityStatusCommand(int Id, bool Activate, string RowVersion)
    : ICommand<AdminCityResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class GetAdminCitiesQueryValidator : AbstractValidator<GetAdminCitiesQuery>
{
    public GetAdminCitiesQueryValidator()
    {
        RuleFor(query => query.GovernorateId).GreaterThan(0).When(query => query.GovernorateId.HasValue);
        RuleFor(query => query.SearchText).MaximumLength(City.MaximumNameLength);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class CreateCityCommandValidator : AbstractValidator<CreateCityCommand>
{
    public CreateCityCommandValidator()
    {
        RuleFor(command => command.GovernorateId).GreaterThan(0);
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(City.MaximumNameLength);
        RuleFor(command => command.NameEn).NotEmpty().MaximumLength(City.MaximumNameLength);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateCityCommandValidator : AbstractValidator<UpdateCityCommand>
{
    public UpdateCityCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);
        RuleFor(command => command.GovernorateId).GreaterThan(0);
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(City.MaximumNameLength);
        RuleFor(command => command.NameEn).NotEmpty().MaximumLength(City.MaximumNameLength);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.RowVersion).Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("City.InvalidRowVersion");
    }
}

internal sealed class ChangeCityStatusCommandValidator : AbstractValidator<ChangeCityStatusCommand>
{
    public ChangeCityStatusCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);
        RuleFor(command => command.RowVersion).Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("City.InvalidRowVersion");
    }
}

internal sealed class GetAdminCitiesQueryHandler(
    IReadRepository<City, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetAdminCitiesQuery, PagedResult<AdminCityResponse>>
{
    public async Task<Result<PagedResult<AdminCityResponse>>> Handle(
        GetAdminCitiesQuery query, CancellationToken cancellationToken)
    {
        var (items, count) = await repository.ListWithLongCountAsync(
            new AdminCitiesSpecification(query), cancellationToken);
        return Result<PagedResult<AdminCityResponse>>.Ok(new(
            query.PageNumber, query.PageSize, count, items.Select(item => item.ToResponse())));
    }
}

internal sealed class GetAdminCityQueryHandler(
    IReadRepository<City, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetAdminCityQuery, AdminCityResponse>
{
    public async Task<Result<AdminCityResponse>> Handle(GetAdminCityQuery query, CancellationToken cancellationToken)
    {
        var item = await repository.FirstOrDefaultAsync(new CityByIdSpecification(query.Id), cancellationToken);
        return item is null ? Result<AdminCityResponse>.Fail(CityErrors.NotFound) : Result<AdminCityResponse>.Ok(item.ToResponse());
    }
}

internal sealed class CreateCityCommandHandler(
    IReadRepository<Governorate, LawyerPlatformReadPersistence> governorates,
    IReadRepository<City, LawyerPlatformReadPersistence> cities,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork)
    : ICommandHandler<CreateCityCommand, AdminCityResponse>
{
    public async Task<Result<AdminCityResponse>> Handle(CreateCityCommand command, CancellationToken cancellationToken)
    {
        var parent = await governorates.FirstOrDefaultAsync(
            new GovernorateNameByIdSpecification(command.GovernorateId), cancellationToken);
        if (parent is null) return Result<AdminCityResponse>.Fail(CityErrors.InvalidGovernorate);
        var conflict = await LocationConflictChecker.FindCityConflictAsync(
            cities, command.GovernorateId, command.NameAr, command.NameEn, null, cancellationToken);
        if (conflict is not null) return Result<AdminCityResponse>.Fail(conflict);
        var ids = await cities.ListAsync(new CityIdsSpecification(), cancellationToken);
        var creation = City.Create(ids.Count == 0 ? 1 : checked(ids.Max() + 1),
            command.GovernorateId, command.NameAr, command.NameEn, command.DisplayOrder);
        if (creation.IsFailure) return Result<AdminCityResponse>.Fail(creation.Errors);
        var item = creation.Value;
        await unitOfWork.WriteRepository<City>().AddAsync(item, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AdminCityResponse>.Ok(CityMapper.ToResponse(item, parent));
    }
}

internal sealed class UpdateCityCommandHandler(
    IReadRepository<Governorate, LawyerPlatformReadPersistence> governorates,
    IReadRepository<City, LawyerPlatformReadPersistence> cities,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager)
    : ICommandHandler<UpdateCityCommand, AdminCityResponse>
{
    public async Task<Result<AdminCityResponse>> Handle(UpdateCityCommand command, CancellationToken cancellationToken)
    {
        var item = await unitOfWork.WriteRepository<City>().GetByIdAsync(command.Id, cancellationToken);
        if (item is null) return Result<AdminCityResponse>.Fail(CityErrors.NotFound);
        var parent = await governorates.FirstOrDefaultAsync(
            new GovernorateNameByIdSpecification(command.GovernorateId), cancellationToken);
        if (parent is null) return Result<AdminCityResponse>.Fail(CityErrors.InvalidGovernorate);
        var conflict = await LocationConflictChecker.FindCityConflictAsync(
            cities, command.GovernorateId, command.NameAr, command.NameEn, command.Id, cancellationToken);
        if (conflict is not null) return Result<AdminCityResponse>.Fail(conflict);
        var rowVersion = RowVersionCodec.Decode(command.RowVersion);
        if (rowVersion.IsFailure) return Result<AdminCityResponse>.Fail(CityErrors.InvalidRowVersion);
        concurrencyTokenManager.SetOriginalRowVersion(item, rowVersion.Value);
        var update = item.Update(command.GovernorateId, command.NameAr, command.NameEn, command.DisplayOrder);
        if (update.IsFailure) return Result<AdminCityResponse>.Fail(update.Errors);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AdminCityResponse>.Ok(CityMapper.ToResponse(item, parent));
    }
}

internal sealed class ChangeCityStatusCommandHandler(
    IReadRepository<Governorate, LawyerPlatformReadPersistence> governorates,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager)
    : ICommandHandler<ChangeCityStatusCommand, AdminCityResponse>
{
    public async Task<Result<AdminCityResponse>> Handle(ChangeCityStatusCommand command, CancellationToken cancellationToken)
    {
        var item = await unitOfWork.WriteRepository<City>().GetByIdAsync(command.Id, cancellationToken);
        if (item is null) return Result<AdminCityResponse>.Fail(CityErrors.NotFound);
        var parent = await governorates.FirstOrDefaultAsync(
            new GovernorateNameByIdSpecification(item.GovernorateId), cancellationToken);
        var rowVersion = RowVersionCodec.Decode(command.RowVersion);
        if (rowVersion.IsFailure) return Result<AdminCityResponse>.Fail(CityErrors.InvalidRowVersion);
        concurrencyTokenManager.SetOriginalRowVersion(item, rowVersion.Value);
        var change = command.Activate ? item.Activate() : item.Deactivate();
        if (change.IsFailure) return Result<AdminCityResponse>.Fail(change.Errors);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AdminCityResponse>.Ok(CityMapper.ToResponse(item, parent!));
    }
}

internal sealed class AdminCitiesSpecification : Specification<City, CitySnapshot>
{
    public AdminCitiesSpecification(GetAdminCitiesQuery query)
    {
        if (query.GovernorateId.HasValue) AddCriteria(item => item.GovernorateId == query.GovernorateId.Value);
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
        Select(item => new CitySnapshot(
            item.Id, item.GovernorateId, item.Governorate.NameAr, item.Governorate.NameEn,
            item.NameAr, item.NameEn, item.DisplayOrder, item.IsActive, item.RowVersion));
    }
}

file static class CityMapper
{
    public static AdminCityResponse ToResponse(City item, GovernorateNameSnapshot parent) => new(
        item.Id, item.GovernorateId, parent.NameAr, parent.NameEn,
        item.NameAr, item.NameEn, item.DisplayOrder, item.IsActive,
        RowVersionCodec.Encode(item.RowVersion));
}
