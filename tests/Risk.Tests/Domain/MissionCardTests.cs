using System.Reflection;
using Risk.Domain.Map;
using Risk.Domain.Missions;

namespace Risk.Tests.Domain;

public class MissionCardTests
{
    [Fact]
    public void ArmyId_has_value_equality()
    {
        var a = new ArmyId(0);
        var b = new ArmyId(0);
        var c = new ArmyId(1);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(0, a.Value);
    }

    [Fact]
    public void OccupyTerritories_is_a_mission_card_with_default_army_minimum()
    {
        MissionCard card = new OccupyTerritories(18, 2);

        var occupy = Assert.IsType<OccupyTerritories>(card);
        Assert.Equal(18, occupy.Count);
        Assert.Equal(2, occupy.MinArmiesPerTerritory);

        var defaulted = new OccupyTerritories(24);
        Assert.Equal(1, defaulted.MinArmiesPerTerritory);
    }

    [Fact]
    public void EliminateArmy_equality_is_driven_by_ArmyId()
    {
        MissionCard sameA = new EliminateArmy(new ArmyId(3));
        MissionCard sameB = new EliminateArmy(new ArmyId(3));
        MissionCard different = new EliminateArmy(new ArmyId(4));

        Assert.IsType<EliminateArmy>(sameA);
        Assert.Equal(sameA, sameB);
        Assert.NotEqual(sameA, different);
    }

    [Fact]
    public void ConquerContinents_is_a_mission_card_with_default_wildcard_count()
    {
        MissionCard card = new ConquerContinents([new ContinentId("EU"), new ContinentId("SA")], 1);

        var conquer = Assert.IsType<ConquerContinents>(card);
        Assert.Equal(1, conquer.WildcardCount);

        var defaulted = new ConquerContinents([new ContinentId("AS"), new ContinentId("SA")]);
        Assert.Equal(0, defaulted.WildcardCount);
    }

    [Fact]
    public void MissionCard_subtypes_are_sealed()
    {
        Assert.True(typeof(OccupyTerritories).IsSealed);
        Assert.True(typeof(EliminateArmy).IsSealed);
        Assert.True(typeof(ConquerContinents).IsSealed);
    }

    [Fact]
    public void MissionCard_hierarchy_is_closed_to_external_assemblies()
    {
        var baseType = typeof(MissionCard);

        var subtypes = baseType.Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(baseType))
            .ToList();

        Assert.Equal(3, subtypes.Count);
        Assert.All(subtypes, t => Assert.True(t.IsSealed));

        var ctor = baseType.GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        Assert.NotNull(ctor);
        Assert.True(ctor!.IsFamilyAndAssembly);
    }
}
