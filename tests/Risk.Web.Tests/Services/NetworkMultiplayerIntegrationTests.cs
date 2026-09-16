using Risk.Domain.Map;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Web.Services;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Services;

/// <summary>
/// Three seats share one <see cref="NetworkGameSession"/> through the real
/// <see cref="GameEngine"/>: lobby claims, host start, then one claim per
/// seat in engine turn order — proof that multi-device play
/// composes through the authoritative session.
/// </summary>
public class NetworkMultiplayerIntegrationTests
{
    [Fact]
    public void ThreeSeats_ClaimOneTerritoryEach_ThroughRealEngine()
    {
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var registry = new NetworkGameRegistry(engine, QueuedDiceRoller.ForRollOff(3));
        var session = registry.Create();
        session.ClaimSeat("Ana", "#FF0000", "c0");
        session.ClaimSeat("Beto", "#00FF00", "c1");
        session.ClaimSeat("Ceci", "#1D4ED8", "c2");

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(session.Start(GameMode.Classic));
        Assert.Equal(TurnPhase.Claim, session.State!.Turn.Phase);

        var notifications = 0;
        session.Changed += () => notifications++;

        var claimed = new List<(Domain.Players.PlayerId Player, TerritoryId Territory)>();
        foreach (var territory in WorldMap.Territories.Take(3).Select(t => t.Id))
        {
            var current = session.State!.Turn.CurrentPlayer;
            var result = session.Dispatch(new ClaimTerritoryCommand(current, territory, 1));
            Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
            claimed.Add((current, territory));
        }

        foreach (var (player, territory) in claimed)
        {
            Assert.Equal(player, session.State!.Territories[territory].Owner);
        }

        Assert.Equal(3, notifications);
    }
}
