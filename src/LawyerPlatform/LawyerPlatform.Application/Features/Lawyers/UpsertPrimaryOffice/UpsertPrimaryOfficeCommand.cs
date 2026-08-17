using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using FluentValidation;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Common.Validation;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.Lawyers.UpsertPrimaryOffice;

public sealed record UpsertPrimaryOfficeCommand(
    int GovernorateId,
    int CityId,
    int AreaId,
    string DetailedAddress,
    string? PublicPhoneNumber,
    string? RowVersion)
    : ICommand<LawyerOfficeResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class UpsertPrimaryOfficeCommandValidator : AbstractValidator<UpsertPrimaryOfficeCommand>
{
    public UpsertPrimaryOfficeCommandValidator()
    {
        RuleFor(command => command.GovernorateId).GreaterThan(0);
        RuleFor(command => command.CityId).GreaterThan(0);
        RuleFor(command => command.AreaId).GreaterThan(0);
        RuleFor(command => command.DetailedAddress).NotEmpty().MaximumLength(500);
        RuleFor(command => command.PublicPhoneNumber)
            .EgyptianMobileNumber()
            .WithErrorCode("Lawyer.InvalidPublicPhoneNumber")
            .When(command => !string.IsNullOrWhiteSpace(command.PublicPhoneNumber));
        RuleFor(command => command.RowVersion)
            .Must(value => value is null || RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Lawyer.InvalidRowVersion");
    }
}

internal sealed class UpsertPrimaryOfficeCommandHandler(
    ICurrentUser currentUser,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IReadRepository<Area, LawyerPlatformReadPersistence> areaReader,
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> profileReader,
    IConcurrencyTokenManager concurrencyTokenManager,
    ILawyerAggregatePersistence aggregatePersistence,
    IDateTimeProvider clock)
    : ICommandHandler<UpsertPrimaryOfficeCommand, LawyerOfficeResponse>
{
    public async Task<Result<LawyerOfficeResponse>> Handle(UpsertPrimaryOfficeCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<LawyerOfficeResponse>.Fail(LawyerApplicationErrors.AccountNotFound);
        }

        var hierarchy = await areaReader.FirstOrDefaultAsync(new LocationHierarchySpecification(command.AreaId), cancellationToken);
        if (hierarchy is null || !hierarchy.AreaIsActive || !hierarchy.CityIsActive || !hierarchy.GovernorateIsActive)
        {
            return Result<LawyerOfficeResponse>.Fail(LawyerApplicationErrors.LocationNotFound);
        }

        if (hierarchy.CityId != command.CityId || hierarchy.GovernorateId != command.GovernorateId)
        {
            return Result<LawyerOfficeResponse>.Fail(LawyerApplicationErrors.InvalidLocationHierarchy);
        }

        var profile = await unitOfWork.WriteRepository<LawyerProfile>()
            .FirstOrDefaultAsync(new LawyerAggregateByUserAccountIdSpecification(userId), cancellationToken);
        if (profile is null)
        {
            return Result<LawyerOfficeResponse>.Fail(LawyerErrors.NotFound);
        }

        var existing = profile.Offices.SingleOrDefault(office => office.IsPrimary && office.IsActive);
        if (existing is null && command.RowVersion is not null)
        {
            return Result<LawyerOfficeResponse>.Fail(LawyerErrors.InvalidRowVersion);
        }

        if (existing is not null)
        {
            var rowVersion = RowVersionCodec.Decode(command.RowVersion);
            if (rowVersion.IsFailure)
            {
                return Result<LawyerOfficeResponse>.Fail(rowVersion.Errors);
            }

            concurrencyTokenManager.SetOriginalRowVersion(existing, rowVersion.Value);
        }

        var upsert = profile.UpsertPrimaryOffice(
            command.GovernorateId,
            command.CityId,
            command.AreaId,
            command.DetailedAddress,
            command.PublicPhoneNumber);
        if (upsert.IsFailure)
        {
            return Result<LawyerOfficeResponse>.Fail(upsert.Errors);
        }

        if (existing is null)
        {
            aggregatePersistence.Add(upsert.Value);
        }

        profile.ModifiedOnUtc = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var office = await profileReader.FirstOrDefaultAsync(new OwnPrimaryOfficeSpecification(userId), cancellationToken);
        return office is null
            ? Result<LawyerOfficeResponse>.Fail(LawyerErrors.OfficeNotFound)
            : Result<LawyerOfficeResponse>.Ok(office.ToResponse());
    }
}

internal sealed class OwnPrimaryOfficeSpecification : Specification<LawyerProfile, PrimaryOfficeSnapshot>
{
    public OwnPrimaryOfficeSpecification(Guid userAccountId)
    {
        AddCriteria(profile => profile.UserAccountId == userAccountId);
        UseNoTracking();
        Select(profile => profile.Offices
            .Where(office => office.IsPrimary && office.IsActive)
            .Select(office => new PrimaryOfficeSnapshot(
                office.Id,
                office.GovernorateId,
                office.Governorate.NameAr,
                office.Governorate.NameEn,
                office.CityId,
                office.City.NameAr,
                office.City.NameEn,
                office.AreaId,
                office.Area.NameAr,
                office.Area.NameEn,
                office.DetailedAddress,
                office.PublicPhoneNumber,
                office.RowVersion))
            .Single());
    }
}

internal sealed record PrimaryOfficeSnapshot(
    Guid Id,
    int GovernorateId,
    string GovernorateNameAr,
    string GovernorateNameEn,
    int CityId,
    string CityNameAr,
    string CityNameEn,
    int AreaId,
    string AreaNameAr,
    string AreaNameEn,
    string DetailedAddress,
    string? PublicPhoneNumber,
    byte[] RowVersion)
{
    public LawyerOfficeResponse ToResponse() => new(
        Id,
        GovernorateId,
        GovernorateNameAr,
        GovernorateNameEn,
        CityId,
        CityNameAr,
        CityNameEn,
        AreaId,
        AreaNameAr,
        AreaNameEn,
        DetailedAddress,
        PublicPhoneNumber,
        RowVersionCodec.Encode(RowVersion));
}
