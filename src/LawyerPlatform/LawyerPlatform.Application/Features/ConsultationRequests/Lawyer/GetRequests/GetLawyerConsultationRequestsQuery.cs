using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.SharedDto;
using BuildingBlock.Domain.Specification;
using FluentValidation;
using LawyerPlatform.Application.Features.ConsultationRequests.Lawyer.Common;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Application.Features.ConsultationRequests.Lawyer.GetRequests;

public sealed record GetLawyerConsultationRequestsQuery(
    ConsultationRequestStatus? Status,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedResult<LawyerConsultationListItemResponse>>;

internal sealed class GetLawyerConsultationRequestsQueryValidator
    : AbstractValidator<GetLawyerConsultationRequestsQuery>
{
    public GetLawyerConsultationRequestsQueryValidator()
    {
        RuleFor(query => query.Status).IsInEnum().When(query => query.Status.HasValue);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class GetLawyerConsultationRequestsQueryHandler(
    ICurrentUser currentUser,
    IReadRepository<ConsultationRequest, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetLawyerConsultationRequestsQuery, PagedResult<LawyerConsultationListItemResponse>>
{
    public async Task<Result<PagedResult<LawyerConsultationListItemResponse>>> Handle(
        GetLawyerConsultationRequestsQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<PagedResult<LawyerConsultationListItemResponse>>.Fail(
                ConsultationRequestErrors.NotFound);
        }

        var (items, count) = await repository.ListWithLongCountAsync(
            new LawyerConsultationRequestsSpecification(userId, query),
            cancellationToken);
        return Result<PagedResult<LawyerConsultationListItemResponse>>.Ok(
            new PagedResult<LawyerConsultationListItemResponse>(
                query.PageNumber,
                query.PageSize,
                count,
                items.Select(item => item.ToResponse())));
    }
}

internal sealed record LawyerConsultationListSnapshot(
    Guid Id,
    string ReferenceNumber,
    bool IsClient,
    string RequesterFullName,
    int? SpecializationId,
    string? SpecializationNameAr,
    string? SpecializationNameEn,
    ConsultationRequestStatus Status,
    DateTime? PreferredAppointmentOnUtc,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    byte[] RowVersion)
{
    public LawyerConsultationListItemResponse ToResponse()
        => new(
            Id,
            ReferenceNumber,
            IsClient ? "Client" : "Guest",
            RequesterFullName,
            SpecializationId.HasValue
                ? new ConsultationSpecializationResponse(
                    SpecializationId.Value,
                    SpecializationNameAr!,
                    SpecializationNameEn!)
                : null,
            Status.ToString(),
            PreferredAppointmentOnUtc,
            CreatedOnUtc,
            ModifiedOnUtc,
            RowVersionCodec.Encode(RowVersion));
}

internal sealed class LawyerConsultationRequestsSpecification
    : Specification<ConsultationRequest, LawyerConsultationListSnapshot>
{
    public LawyerConsultationRequestsSpecification(
        Guid userAccountId,
        GetLawyerConsultationRequestsQuery query)
    {
        AddCriteria(request => request.LawyerProfile.UserAccountId == userAccountId);
        if (query.Status.HasValue)
        {
            AddCriteria(request => request.Status == query.Status.Value);
        }

        AddOrderByDescending(request => request.CreatedOnUtc);
        AddOrderByDescending(request => request.Id);
        ApplyPaging(query.PageNumber, query.PageSize, 100);
        UseNoTracking();
        Select(request => new LawyerConsultationListSnapshot(
            request.Id,
            request.ReferenceNumber,
            request.ClientProfileId != null,
            request.ClientProfileId != null ? request.ClientProfile!.FullName : request.GuestFullName!,
            request.LegalSpecializationId,
            request.LegalSpecialization != null ? request.LegalSpecialization.NameAr : null,
            request.LegalSpecialization != null ? request.LegalSpecialization.NameEn : null,
            request.Status,
            request.PreferredAppointmentOnUtc,
            request.CreatedOnUtc,
            request.ModifiedOnUtc,
            request.RowVersion));
    }
}
