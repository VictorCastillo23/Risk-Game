using Risk.Domain.Errors;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

public class GameEngineValidationTests
{
    [Fact]
    public void Execute_rejects_a_command_from_a_player_who_is_not_the_active_player()
    {
        var state = GameStateBuilder.CompleteSetup(2);
        var engine = new GameEngine();
        var inactivePlayer = state.Players.Single(p => p.Id != state.Turn.CurrentPlayer).Id;
        var territory = state.Territories.First(kv => kv.Value.Owner == inactivePlayer).Key;

        var result = engine.Execute(state, new PlaceTroopsCommand(inactivePlayer, territory, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotYourTurn, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_an_attack_command_issued_during_the_reinforce_phase()
    {
        var state = GameStateBuilder.CompleteSetup(2);
        var engine = new GameEngine();
        var actor = state.Turn.CurrentPlayer;
        var (from, to) = AdjacentPairOwnedBy(state, actor);

        var result = engine.Execute(state, new AttackCommand(actor, from, to, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.WrongPhase, rejection.Error.Code);
    }

    private static (TerritoryId From, TerritoryId To) AdjacentPairOwnedBy(GameState state, PlayerId actor)
    {
        var from = state.Territories.First(kv => kv.Value.Owner == actor).Key;
        var to = WorldMap.NeighborsOf(from).First();
        return (from, to);
    }
}
