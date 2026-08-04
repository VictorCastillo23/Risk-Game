using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Setup;
using Risk.Engine.State;

namespace Risk.Tests.Fakes;

/// <summary>
/// Test-only helper that drives a fresh game through the turn-based initial
/// placement loop, so engine tests can start directly from the Reinforce
/// phase without re-implementing the placement loop in every test.
/// </summary>
internal static class GameStateBuilder
{
    public static GameState CompleteSetup(int playerCount)
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(GameSetup.Create(playerCount));
        var state = ok.State;
        var engine = new GameEngine();

        while (state.Players.Any(p => p.TroopsRemaining > 0))
        {
            var actor = state.Turn.CurrentPlayer;
            var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new PlaceTroopsCommand(actor, territory, 1)));
            state = result.State;
        }

        return state;
    }
}
