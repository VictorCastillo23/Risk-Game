using Risk.Domain.Cards;
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

/// <summary>
/// Roadmap item 4.2: the <see cref="GameMode.TwoPlayer"/> neutral army
/// (<see cref="PlayerState.IsNeutral"/>) never holds a turn and never acts.
/// Mirrors <see cref="EliminationTests"/>'s hand-built-<see cref="GameState"/>
/// style (design D4).
/// </summary>
public class NeutralPlayerGuardTests
{
    [Fact]
    public void AdvanceToNextPlayer_skips_a_neutral_player_when_rotating_the_turn()
    {
        var human0 = new PlayerId(0);
        var neutral1 = new PlayerId(1);
        var human2 = new PlayerId(2);
        var territories = WorldMap.Territories.ToDictionary(t => t.Id, t => new TerritoryState(human0, 1));

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(human0, [], IsEliminated: false, TroopsRemaining: 0),
            new PlayerState(neutral1, [], IsEliminated: false, TroopsRemaining: 10, IsNeutral: true),
            new PlayerState(human2, [], IsEliminated: false, TroopsRemaining: 0)
        ];

        var state = new GameState(
            territories, players, new TurnState(human0, TurnPhase.Fortify), Deck.CreateStandard(), [], new GameStatus.InProgress());
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(engine.Execute(state, new EndPhaseCommand(human0)));

        Assert.Equal(human2, result.State.Turn.CurrentPlayer);
    }

    [Fact]
    public void AdvanceToNextPlayer_skips_both_an_eliminated_player_and_a_neutral_player()
    {
        var p1 = new PlayerId(0);
        var p2Eliminated = new PlayerId(1);
        var neutral = new PlayerId(2);
        var territories = WorldMap.Territories.ToDictionary(t => t.Id, t => new TerritoryState(p1, 1));

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(p1, [], IsEliminated: false, TroopsRemaining: 0),
            new PlayerState(p2Eliminated, [], IsEliminated: true, TroopsRemaining: 0),
            new PlayerState(neutral, [], IsEliminated: false, TroopsRemaining: 10, IsNeutral: true)
        ];

        var state = new GameState(
            territories, players, new TurnState(p1, TurnPhase.Fortify), Deck.CreateStandard(), [], new GameStatus.InProgress());
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(engine.Execute(state, new EndPhaseCommand(p1)));

        Assert.Equal(p1, result.State.Turn.CurrentPlayer);
    }

    [Fact]
    public void AdvanceToNextPlayer_never_grants_reinforcement_to_the_skipped_neutral()
    {
        var human0 = new PlayerId(0);
        var neutral1 = new PlayerId(1);
        var human2 = new PlayerId(2);
        var territories = WorldMap.Territories.ToDictionary(t => t.Id, t => new TerritoryState(human0, 1));
        const int neutralTroopsBefore = 10;

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(human0, [], IsEliminated: false, TroopsRemaining: 0),
            new PlayerState(neutral1, [], IsEliminated: false, TroopsRemaining: neutralTroopsBefore, IsNeutral: true),
            new PlayerState(human2, [], IsEliminated: false, TroopsRemaining: 0)
        ];

        var state = new GameState(
            territories, players, new TurnState(human0, TurnPhase.Fortify), Deck.CreateStandard(), [], new GameStatus.InProgress());
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(engine.Execute(state, new EndPhaseCommand(human0)));

        Assert.Equal(neutralTroopsBefore, result.State.Players.Single(p => p.IsNeutral).TroopsRemaining);
        Assert.NotEqual(neutral1, result.State.Turn.CurrentPlayer);
    }

    /// <summary>
    /// Secondary, defense-in-depth guard (spec's SECONDARY scenario): even a
    /// hand-forged state where <see cref="TurnState.CurrentPlayer"/> is
    /// artificially set to the neutral's id — a shape that should not occur
    /// once the rotation fix lands — is still rejected. This is not the
    /// primary justification for the gate; see
    /// <c>TwoPlayerSetupTests.PlaceNeutralTroopsCommand_rejects_neutral_as_actor</c>
    /// for the reachable, real-caller-path coverage.
    /// </summary>
    [Fact]
    public void Execute_rejects_any_command_when_CurrentPlayer_is_artificially_the_neutral()
    {
        var neutral = new PlayerId(1);
        var human = new PlayerId(0);
        var territories = WorldMap.Territories.ToDictionary(t => t.Id, t => new TerritoryState(human, 1));

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(human, [], IsEliminated: false, TroopsRemaining: 0),
            new PlayerState(neutral, [], IsEliminated: false, TroopsRemaining: 10, IsNeutral: true)
        ];

        var state = new GameState(
            territories, players, new TurnState(neutral, TurnPhase.Fortify), Deck.CreateStandard(), [], new GameStatus.InProgress());
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(neutral));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.ActorIsNeutral, rejection.Error.Code);
    }

    /// <summary>
    /// Scope guard: the new actor-is-neutral gate must not swallow the
    /// existing not-your-turn check for ordinary (non-neutral) actors.
    /// </summary>
    [Fact]
    public void Execute_still_rejects_a_non_neutral_actor_acting_out_of_turn()
    {
        var human0 = new PlayerId(0);
        var human1 = new PlayerId(1);
        var neutral = new PlayerId(2);
        var territories = WorldMap.Territories.ToDictionary(t => t.Id, t => new TerritoryState(human0, 1));

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(human0, [], IsEliminated: false, TroopsRemaining: 0),
            new PlayerState(human1, [], IsEliminated: false, TroopsRemaining: 0),
            new PlayerState(neutral, [], IsEliminated: false, TroopsRemaining: 10, IsNeutral: true)
        ];

        var state = new GameState(
            territories, players, new TurnState(human0, TurnPhase.Fortify), Deck.CreateStandard(), [], new GameStatus.InProgress());
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(human1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotYourTurn, rejection.Error.Code);
    }
}
