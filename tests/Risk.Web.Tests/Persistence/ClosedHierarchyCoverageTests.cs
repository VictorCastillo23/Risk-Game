using Risk.Domain.Cards;
using Risk.Domain.Missions;
using Risk.Engine.Events;
using Risk.Engine.State;
using Risk.Web.Persistence;

namespace Risk.Web.Tests.Persistence;

/// <summary>
/// Task 4.3: a reflection-based guard proving <see cref="ClosedHierarchyResolver"/>
/// registers a discriminator for every concrete, non-abstract subtype of
/// each of this codebase's 4 closed hierarchies (<see cref="Card"/>,
/// <see cref="GameStatus"/>, <see cref="MissionCard"/>, <see cref="GameEvent"/>).
/// This is the only test in the suite that fails automatically when someone
/// adds a 5th <see cref="Card"/> variant (or a 17th <see cref="GameEvent"/>,
/// etc.) without also registering it in <see cref="ClosedHierarchyResolver.Modify"/>
/// — every other round-trip test only proves the variants it happens to
/// construct, not the hierarchy's total membership.
/// </summary>
public class ClosedHierarchyCoverageTests
{
    public static IEnumerable<object[]> Hierarchies()
    {
        yield return [typeof(Card)];
        yield return [typeof(GameStatus)];
        yield return [typeof(MissionCard)];
        yield return [typeof(GameEvent)];
    }

    [Theory]
    [MemberData(nameof(Hierarchies))]
    public void Registered_discriminator_count_matches_concrete_subtype_count(Type hierarchyBaseType)
    {
        var typeInfo = GameJson.Options.GetTypeInfo(hierarchyBaseType);
        var polymorphism = typeInfo.PolymorphismOptions
            ?? throw new InvalidOperationException(
                $"{hierarchyBaseType.Name} has no PolymorphismOptions attached — " +
                $"{nameof(ClosedHierarchyResolver)}.{nameof(ClosedHierarchyResolver.Modify)} no longer registers this hierarchy.");

        var registeredCount = polymorphism.DerivedTypes.Count;

        // "Concrete, non-abstract subtype in the defining assembly" is the
        // same scope ClosedHierarchyResolver draws from (Card/MissionCard
        // live in Risk.Domain, GameStatus/GameEvent live in Risk.Engine) —
        // scanning the whole assembly, not just direct children, so a future
        // deeper hierarchy (e.g. an intermediate abstract type) is still
        // caught correctly.
        var concreteSubtypeCount = hierarchyBaseType.Assembly.GetTypes()
            .Count(t => t != hierarchyBaseType && hierarchyBaseType.IsAssignableFrom(t) && !t.IsAbstract);

        Assert.Equal(concreteSubtypeCount, registeredCount);
    }

    [Theory]
    [MemberData(nameof(Hierarchies))]
    public void Discriminator_uses_dollar_kind_and_does_not_ignore_unrecognized_types(Type hierarchyBaseType)
    {
        var typeInfo = GameJson.Options.GetTypeInfo(hierarchyBaseType);
        var polymorphism = typeInfo.PolymorphismOptions
            ?? throw new InvalidOperationException($"{hierarchyBaseType.Name} has no PolymorphismOptions attached.");

        Assert.Equal("$kind", polymorphism.TypeDiscriminatorPropertyName);

        // Must stay false: an unregistered/future variant must throw loudly
        // on serialize/deserialize (confirmed empirically in PR3's fresh-
        // context review), never silently drop data.
        Assert.False(polymorphism.IgnoreUnrecognizedTypeDiscriminators);
    }
}
