using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Events;
using Risk.Engine.State;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class BattleEventSelectorTests
{
    private static readonly PlayerId Attacker = new(0);
    private static readonly PlayerId Defender = new(1);
    private static readonly TerritoryId From = new("Alaska");
    private static readonly TerritoryId To = new("Alberta");

    [Fact]
    public void MostRecent_WithEmptyList_ReturnsNull()
    {
        Assert.Null(BattleEventSelector.MostRecent([]));
    }

    [Fact]
    public void MostRecent_WithNoBattleResolvedEvent_ReturnsNull()
    {
        var events = new GameEvent[]
        {
            new PhaseChanged(TurnPhase.Reinforce, TurnPhase.Attack, Attacker),
        };

        Assert.Null(BattleEventSelector.MostRecent(events));
    }

    [Fact]
    public void MostRecent_WithOneBattleResolvedEvent_ReturnsIt()
    {
        var battle = new BattleResolved(Attacker, From, To, [6, 4], [3], 0, 1);
        var events = new GameEvent[] { battle };

        Assert.Same(battle, BattleEventSelector.MostRecent(events));
    }

    [Fact]
    public void MostRecent_WithBattleResolvedFollowedByOtherEvents_StillReturnsIt()
    {
        var battle = new BattleResolved(Attacker, From, To, [6, 4], [3], 0, 1);
        var events = new GameEvent[]
        {
            battle,
            new TerritoryConquered(Attacker, Defender, To),
        };

        Assert.Same(battle, BattleEventSelector.MostRecent(events));
    }

    [Fact]
    public void MostRecent_WithMultipleBattleResolvedEvents_ReturnsTheLastOne()
    {
        var first = new BattleResolved(Attacker, From, To, [6], [1], 1, 0);
        var second = new BattleResolved(Attacker, From, To, [5, 5], [2], 0, 2);
        var events = new GameEvent[] { first, second };

        Assert.Same(second, BattleEventSelector.MostRecent(events));
    }
}
