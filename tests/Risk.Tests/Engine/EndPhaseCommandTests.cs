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

public class EndPhaseCommandTests
{
    [Fact]
    public void Execute_advances_from_reinforce_to_attack_for_the_same_player()
    {
        var state = GameStateBuilder.CompleteSetup(2);
        var actor = state.Turn.CurrentPlayer;
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(actor));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(TurnPhase.Attack, ok.State.Turn.Phase);
        Assert.Equal(actor, ok.State.Turn.CurrentPlayer);
    }

    [Fact]
    public void Execute_advances_from_attack_to_fortify_for_the_same_player()
    {
        var setup = GameStateBuilder.CompleteSetup(2);
        var actor = setup.Turn.CurrentPlayer;
        var engine = new GameEngine(new QueuedDiceRoller());
        var reinforceToAttack = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(setup, new EndPhaseCommand(actor)));

        var result = engine.Execute(reinforceToAttack.State, new EndPhaseCommand(actor));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(TurnPhase.Fortify, ok.State.Turn.Phase);
        Assert.Equal(actor, ok.State.Turn.CurrentPlayer);
    }

    [Fact]
    public void Execute_advancing_past_fortify_rotates_to_the_next_players_reinforce_phase_and_resets_per_turn_flags()
    {
        var actor = new PlayerId(0);
        var next = new PlayerId(1);
        var state = BuildFortifyPhaseState(actor, next, conqueredThisTurn: true, fortifyUsed: true);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(actor));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(next, ok.State.Turn.CurrentPlayer);
        Assert.Equal(TurnPhase.Reinforce, ok.State.Turn.Phase);
        Assert.False(ok.State.Turn.ConqueredThisTurn); // reset for the new turn
        Assert.False(ok.State.Turn.FortifyUsed);        // reset for the new turn
        Assert.Null(ok.State.Turn.PendingOccupation);
    }

    [Fact]
    public void Execute_advancing_past_fortify_assigns_the_next_players_reinforcement_troops()
    {
        var actor = new PlayerId(0);
        var next = new PlayerId(1);
        // "next" owns exactly 6 territories and no continent -> floor(6/3)=2,
        // below the minimum of 3, so reinforcement must be exactly 3.
        var state = BuildFortifyPhaseState(actor, next, conqueredThisTurn: false, fortifyUsed: false);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(actor));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var nextPlayerState = ok.State.Players.Single(p => p.Id == next);
        Assert.Equal(3, nextPlayerState.TroopsRemaining);
    }

    [Fact]
    public void Execute_rejects_end_phase_from_a_player_who_is_not_the_active_player()
    {
        var state = GameStateBuilder.CompleteSetup(2);
        var inactivePlayer = state.Players.Single(p => p.Id != state.Turn.CurrentPlayer).Id;
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(inactivePlayer));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotYourTurn, rejection.Error.Code);
    }

    /// <summary>
    /// Builds a minimal 2-player state where <paramref name="actor"/> is in
    /// the Fortify phase and owns every territory except the 6 North
    /// American territories, which belong to <paramref name="next"/> (6
    /// territories, no full continent, so reinforcement is the floor-based
    /// minimum of 3).
    /// </summary>
    private static GameState BuildFortifyPhaseState(PlayerId actor, PlayerId next, bool conqueredThisTurn, bool fortifyUsed)
    {
        TerritoryId[] nextTerritories =
        [
            new("Alaska"), new("NorthwestTerritory"), new("Greenland"),
            new("Alberta"), new("Ontario"), new("Quebec")
        ];

        var territories = new Dictionary<TerritoryId, TerritoryState>();
        foreach (var territory in WorldMap.Territories)
        {
            var owner = nextTerritories.Contains(territory.Id) ? next : actor;
            territories[territory.Id] = new TerritoryState(owner, 1);
        }

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(actor, [], false, 0),
            new PlayerState(next, [], false, 0)
        ];

        var turn = new TurnState(actor, TurnPhase.Fortify, ConqueredThisTurn: conqueredThisTurn, FortifyUsed: fortifyUsed);

        return new GameState(territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress());
    }
}
