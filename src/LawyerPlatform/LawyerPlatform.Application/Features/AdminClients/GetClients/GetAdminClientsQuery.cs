using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.SharedDto;
using FluentValidation;
using LawyerPlatform.Application.Features.AdminClients.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Clients;

namespace LawyerPlatform.Application.Features.AdminClients.GetClients;

public sealed record GetAdminClientsQuery(int PageNumber = 1, int PageSize = 20)
    : IQuery<PagedResult<AdminClientListItemResponse>>;

internal sealed class GetAdminClientsQueryValidator : AbstractValidator<GetAdminClientsQuery>
{
    public GetAdminClientsQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class GetAdminClientsQueryHandler(
    IReadRepository<ClientProfile, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetAdminClientsQuery, PagedResult<AdminClientListItemResponse>>
{
    public async Task<Result<PagedResult<AdminClientListItemResponse>>> Handle(
        GetAdminClientsQuery query,
        CancellationToken cancellationToken)
    {
        var (items, count) = await repository.ListWithLongCountAsync(
            new AdminClientsSpecification(query.PageNumber, query.PageSize),
            cancellationToken);
        return Result<PagedResult<AdminClientListItemResponse>>.Ok(
            new PagedResult<AdminClientListItemResponse>(
                query.PageNumber,
                query.PageSize,
                count,
                items.Select(item => item.ToResponse())));
    }
}
