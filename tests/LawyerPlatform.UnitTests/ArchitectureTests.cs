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
        Assert.DoesNotContain("Microsoft.AspNetCore", references);
        Assert.DoesNotContain("Microsoft.AspNetCore.Core", references);
        Assert.DoesNotContain("MediatR", references);
    }

    [Fact]
    public void ProductionAssemblies_DoNotContainStarterCatalogTypes()
    {
        Assert.DoesNotContain(
            typeof(UserAccount).Assembly.GetTypes(),
            type => type.Namespace?.Contains(".Catalog", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(
            typeof(LawyerPlatform.Application.AssemblyReference).Assembly.GetTypes(),
            type => type.Namespace?.Contains(".Catalog", StringComparison.Ordinal) == true);
    }
}
