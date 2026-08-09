using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.SharedDto;
using BuildingBlock.Domain.Specification;
using FluentValidation;
using LawyerPlatform.Application.Features.ConsultationRequests.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Application.Features.ConsultationRequests.Client.GetRequests;

public sealed record GetClientConsultationRequestsQuery(
    ConsultationRequestStatus? Status,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedResult<ClientConsultationListItemResponse>>;

public sealed record ClientConsultationListItemResponse(
    Guid Id,
    string ReferenceNumber,
    ConsultationLawyerSummaryResponse Lawyer,
    ConsultationReferenceSummaryResponse? LegalSpecialization,
    string Status,
    DateTime? PreferredAppointmentOnUtc,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc);

internal sealed class GetClientConsultationRequestsQueryValidator
    : AbstractValidator<GetClientConsultationRequestsQuery>
{
    public GetClientConsultationRequestsQueryValidator()
    {
        RuleFor(query => query.Status).IsInEnum().When(query => query.Status.HasValue);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class GetClientConsultationRequestsQueryHandler(
    ICurrentUser currentUser,
    IReadRepository<ConsultationRequest, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetClientConsultationRequestsQuery, PagedResult<ClientConsultationListItemResponse>>
{
    public async Task<Result<PagedResult<ClientConsultationListItemResponse>>> Handle(
        GetClientConsultationRequestsQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<PagedResult<ClientConsultationListItemResponse>>.Fail(
                ConsultationRequestErrors.NotFound);
        }

        var (items, count) = await repository.ListWithLongCountAsync(
            new ClientConsultationRequestsSpecification(userId, query),
            cancellationToken);
        return Result<PagedResult<ClientConsultationListItemResponse>>.Ok(
            new PagedResult<ClientConsultationListItemResponse>(
                query.PageNumber,
                query.PageSize,
                count,
                items));
    }
}

internal sealed class ClientConsultationRequestsSpecification
    : Specification<ConsultationRequest, ClientConsultationListItemResponse>
{
    public ClientConsultationRequestsSpecification(
        Guid userAccountId,
        GetClientConsultationRequestsQuery query)
    {
        AddCriteria(request =>
            request.ClientProfile != null && request.ClientProfile.UserAccountId == userAccountId);
        if (query.Status.HasValue)
        {
            AddCriteria(request => request.Status == query.Status.Value);
        }

        AddOrderByDescending(request => request.CreatedOnUtc);
        AddOrderByDescending(request => request.Id);
        ApplyPaging(query.PageNumber, query.PageSize, 100);
        UseNoTracking();
        Select(request => new ClientConsultationListItemResponse(
            request.Id,
            request.ReferenceNumber,
            new ConsultationLawyerSummaryResponse(
                request.LawyerProfileId,
                request.LawyerProfile.FullName,
                request.LawyerProfile.ProfessionalTitle),
            request.LegalSpecialization == null
                ? null
                : new ConsultationReferenceSummaryResponse(
                    request.LegalSpecialization.Id,
                    request.LegalSpecialization.NameAr,
                    request.LegalSpecialization.NameEn),
            request.Status.ToString(),
            request.PreferredAppointmentOnUtc,
            request.CreatedOnUtc,
            request.ModifiedOnUtc));
    }
}
