using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Clients;

namespace LawyerPlatform.Application.Features.Clients.Profile;

public sealed record GetOwnClientProfileQuery : IQuery<ClientOwnProfileResponse>;

internal sealed class GetOwnClientProfileQueryHandler(
    ICurrentUser currentUser,
    IReadRepository<ClientProfile, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetOwnClientProfileQuery, ClientOwnProfileResponse>
{
    public async Task<Result<ClientOwnProfileResponse>> Handle(
        GetOwnClientProfileQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<ClientOwnProfileResponse>.Fail(ClientErrors.NotFound);
        }

        var profile = await repository.FirstOrDefaultAsync(
            new ClientOwnProfileSpecification(userId),
            cancellationToken);
        return profile is null
            ? Result<ClientOwnProfileResponse>.Fail(ClientErrors.NotFound)
            : Result<ClientOwnProfileResponse>.Ok(profile.ToResponse());
    }
}
