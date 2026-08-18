using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.SharedDto;
using BuildingBlock.Domain.Specification;
using FluentValidation;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Application.Features.ConsultationRequests.Admin.GetRequests;

public sealed record GetAdminConsultationRequestsQuery(
    string? SearchText,
    ConsultationRequestStatus? Status,
    Guid? LawyerId,
    Guid? ClientProfileId,
    string? RequesterType,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResult<AdminConsultationListItemResponse>>;

public sealed record AdminConsultationListItemResponse(
    Guid Id,
    string ReferenceNumber,
    string RequesterType,
    string RequesterFullName,
    Guid LawyerId,
    string LawyerFullName,
    string ConsultationType,
    decimal? ConsultationPrice,
    string Status,
    DateTime? PreferredAppointmentOnUtc,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    string RowVersion);

internal sealed class GetAdminConsultationRequestsQueryValidator
    : AbstractValidator<GetAdminConsultationRequestsQuery>
{
    public GetAdminConsultationRequestsQueryValidator()
    {
        RuleFor(query => query.Status).IsInEnum().When(query => query.Status.HasValue);
        RuleFor(query => query.RequesterType)
            .Must(value => value is null || value.Equals("Client", StringComparison.OrdinalIgnoreCase) ||
                           value.Equals("Guest", StringComparison.OrdinalIgnoreCase));
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.CreatedToUtc)
            .GreaterThanOrEqualTo(query => query.CreatedFromUtc)
            .When(query => query.CreatedFromUtc.HasValue && query.CreatedToUtc.HasValue);
    }
}

internal sealed class GetAdminConsultationRequestsQueryHandler(
    IReadRepository<ConsultationRequest, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetAdminConsultationRequestsQuery, PagedResult<AdminConsultationListItemResponse>>
{
    public async Task<Result<PagedResult<AdminConsultationListItemResponse>>> Handle(
        GetAdminConsultationRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var (items, count) = await repository.ListWithLongCountAsync(
            new AdminConsultationRequestsSpecification(query), cancellationToken);
        return Result<PagedResult<AdminConsultationListItemResponse>>.Ok(new(
            query.PageNumber,
            query.PageSize,
            count,
            items.Select(item => item.ToResponse()).ToArray()));
    }
}

internal sealed record AdminConsultationListSnapshot(
    Guid Id,
    string ReferenceNumber,
    bool IsClient,
    string RequesterFullName,
    Guid LawyerId,
    string LawyerFullName,
    ConsultationType ConsultationType,
    decimal? ConsultationPrice,
    ConsultationRequestStatus Status,
    DateTime? PreferredAppointmentOnUtc,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    byte[] RowVersion)
{
    public AdminConsultationListItemResponse ToResponse() => new(
        Id, ReferenceNumber, IsClient ? "Client" : "Guest", RequesterFullName,
        LawyerId, LawyerFullName, ConsultationType.ToString(), ConsultationPrice,
        Status.ToString(), PreferredAppointmentOnUtc,
        CreatedOnUtc, ModifiedOnUtc, Convert.ToBase64String(RowVersion));
}

internal sealed class AdminConsultationRequestsSpecification
    : Specification<ConsultationRequest, AdminConsultationListSnapshot>
{
    public AdminConsultationRequestsSpecification(GetAdminConsultationRequestsQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var search = query.SearchText.Trim();
            AddCriteria(request => request.ReferenceNumber.Contains(search) ||
                (request.ClientProfileId != null && request.ClientProfile!.FullName.Contains(search)) ||
                (request.ClientProfileId == null && request.GuestFullName!.Contains(search)) ||
                request.LawyerProfile.FullName.Contains(search));
        }
        if (query.Status.HasValue) AddCriteria(request => request.Status == query.Status.Value);
        if (query.LawyerId.HasValue) AddCriteria(request => request.LawyerProfileId == query.LawyerId.Value);
        if (query.ClientProfileId.HasValue) AddCriteria(request => request.ClientProfileId == query.ClientProfileId.Value);
        if (query.RequesterType?.Equals("Client", StringComparison.OrdinalIgnoreCase) == true)
            AddCriteria(request => request.ClientProfileId != null);
        if (query.RequesterType?.Equals("Guest", StringComparison.OrdinalIgnoreCase) == true)
            AddCriteria(request => request.ClientProfileId == null);
        if (query.CreatedFromUtc.HasValue) AddCriteria(request => request.CreatedOnUtc >= query.CreatedFromUtc.Value);
        if (query.CreatedToUtc.HasValue) AddCriteria(request => request.CreatedOnUtc <= query.CreatedToUtc.Value);
        AddOrderByDescending(request => request.CreatedOnUtc);
        AddOrderByDescending(request => request.Id);
        ApplyPaging(query.PageNumber, query.PageSize, 100);
        UseNoTracking();
        Select(request => new AdminConsultationListSnapshot(
            request.Id,
            request.ReferenceNumber,
            request.ClientProfileId != null,
            request.ClientProfileId != null ? request.ClientProfile!.FullName : request.GuestFullName!,
            request.LawyerProfileId,
            request.LawyerProfile.FullName,
            request.ConsultationType,
            request.ConsultationPrice,
            request.Status,
            request.PreferredAppointmentOnUtc,
            request.CreatedOnUtc,
            request.ModifiedOnUtc,
            request.RowVersion));
    }
}
