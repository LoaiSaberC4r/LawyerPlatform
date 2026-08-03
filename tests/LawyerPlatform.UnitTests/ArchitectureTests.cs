using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.UnitTests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Domain_DoesNotReferenceOuterLayers()
    {
        var references = typeof(UserAccount).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("LawyerPlatform.Application", references);
        Assert.DoesNotContain("LawyerPlatform.Infrastructure", references);
        Assert.DoesNotContain("LawyerPlatform.Api", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        Assert.DoesNotContain("MediatR", references);
    }
}
