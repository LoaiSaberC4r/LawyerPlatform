using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Features.AdminClients.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Clients;

namespace LawyerPlatform.Application.Features.AdminClients.GetClient;

public sealed record GetAdminClientQuery(Guid ClientId) : IQuery<AdminClientDetailsResponse>;

internal sealed class GetAdminClientQueryHandler(
    IReadRepository<ClientProfile, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetAdminClientQuery, AdminClientDetailsResponse>
{
    public async Task<Result<AdminClientDetailsResponse>> Handle(
        GetAdminClientQuery query,
        CancellationToken cancellationToken)
    {
        var client = await repository.FirstOrDefaultAsync(
            new AdminClientDetailsSpecification(query.ClientId),
            cancellationToken);
        return client is null
            ? Result<AdminClientDetailsResponse>.Fail(ClientErrors.NotFound)
            : Result<AdminClientDetailsResponse>.Ok(client.ToResponse());
    }
}
