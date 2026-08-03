using BuildingBlock.Domain.Specification;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.Common;

internal sealed class LawyerAggregateByUserAccountIdSpecification : Specification<LawyerProfile>
{
    public LawyerAggregateByUserAccountIdSpecification(Guid userAccountId)
    {
        AddCriteria(profile => profile.UserAccountId == userAccountId);
        AddInclude(profile => profile.UserAccount);
        AddInclude(profile => profile.Offices);
        AddInclude(profile => profile.Specializations);
        AddInclude(profile => profile.Documents);
        AddInclude(profile => profile.StatusHistory);
        UseTracking();
        UseSplitQuery();
    }
}

internal sealed class LawyerAggregateByIdSpecification : Specification<LawyerProfile>
{
    public LawyerAggregateByIdSpecification(Guid lawyerId)
    {
        AddCriteria(profile => profile.Id == lawyerId);
        AddInclude(profile => profile.UserAccount);
        AddInclude(profile => profile.Offices);
        AddInclude(profile => profile.Specializations);
        AddInclude(profile => profile.Documents);
        AddInclude(profile => profile.StatusHistory);
        UseTracking();
        UseSplitQuery();
    }
}
