using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Features.Lawyers.ConsultationSettings.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.ConsultationSettings.GetSettings;

public sealed record GetLawyerConsultationSettingsQuery
    : IQuery<LawyerConsultationSettingsResponse>;

internal sealed class GetLawyerConsultationSettingsQueryHandler(
    ICurrentUser currentUser,
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> lawyerReader,
    IReadRepository<LawyerConsultationSettings, LawyerPlatformReadPersistence> settingsReader)
    : IQueryHandler<GetLawyerConsultationSettingsQuery, LawyerConsultationSettingsResponse>
{
    public async Task<Result<LawyerConsultationSettingsResponse>> Handle(
        GetLawyerConsultationSettingsQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userAccountId)
        {
            return Result<LawyerConsultationSettingsResponse>.Fail(LawyerErrors.NotFound);
        }

        var lawyer = await lawyerReader.FirstOrDefaultAsync(
            new CurrentLawyerIdSpecification(userAccountId),
            cancellationToken);
        if (lawyer is null)
        {
            return Result<LawyerConsultationSettingsResponse>.Fail(LawyerErrors.NotFound);
        }

        var settings = await settingsReader.FirstOrDefaultAsync(
            new LawyerConsultationSettingsByLawyerIdSpecification(lawyer.Id),
            cancellationToken);
        return Result<LawyerConsultationSettingsResponse>.Ok(
            settings?.ToResponse() ?? new LawyerConsultationSettingsResponse(null, [], null));
    }
}
