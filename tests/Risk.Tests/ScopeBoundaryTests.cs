namespace Risk.Tests;

/// <summary>
/// Enforces the spec's "Scope Boundary" requirement: this change must not
/// introduce <c>Risk.AI</c>, <c>Risk.Web</c>, or any other out-of-scope
/// project. Verified via assembly metadata (not just directory listing) so
/// it catches an accidental project reference even if a stray folder
/// existed without being wired into the solution.
/// </summary>
public class ScopeBoundaryTests
{
    [Fact]
    public void Risk_Engine_references_no_out_of_scope_assemblies()
    {
        AssertNoOutOfScopeReferences(typeof(Risk.Engine.GameEngine).Assembly);
    }

    [Fact]
    public void Risk_Domain_references_no_out_of_scope_assemblies()
    {
        AssertNoOutOfScopeReferences(typeof(Risk.Domain.Map.WorldMap).Assembly);
    }

    private static void AssertNoOutOfScopeReferences(System.Reflection.Assembly assembly)
    {
        var referencedNames = assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.DoesNotContain("Risk.AI", referencedNames);
        Assert.DoesNotContain("Risk.Web", referencedNames);
    }
}
