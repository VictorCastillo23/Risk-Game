using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine.Rules;

namespace Risk.Tests.Rules;

public class MissionResolutionTests
{
    private static readonly PlayerId PlayerA = new(0);
    private static readonly PlayerId PlayerB = new(1);

    [Fact]
    public void Effective_substitutes_a_self_targeting_EliminateArmy_with_OccupyTerritories_24_1()
    {
        var dealt = new EliminateArmy(new ArmyId(PlayerA.Value));

        var effective = MissionResolution.Effective(PlayerA, dealt);

        Assert.Equal(new OccupyTerritories(24, MinArmiesPerTerritory: 1), effective);
    }

    [Fact]
    public void Effective_returns_an_other_seat_targeting_EliminateArmy_unchanged()
    {
        var dealt = new EliminateArmy(new ArmyId(PlayerB.Value));

        var effective = MissionResolution.Effective(PlayerA, dealt);

        Assert.Same(dealt, effective);
    }

    [Fact]
    public void Effective_returns_OccupyTerritories_unchanged()
    {
        var dealt = new OccupyTerritories(18, MinArmiesPerTerritory: 2);

        var effective = MissionResolution.Effective(PlayerA, dealt);

        Assert.Same(dealt, effective);
    }

    [Fact]
    public void Effective_returns_ConquerContinents_unchanged()
    {
        var dealt = new ConquerContinents(Required: [new ContinentId("NA")], WildcardCount: 1);

        var effective = MissionResolution.Effective(PlayerA, dealt);

        Assert.Same(dealt, effective);
    }
}
