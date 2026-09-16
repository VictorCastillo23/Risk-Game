using Risk.Domain.Cards;
using Risk.Domain.Errors;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Rules;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

public class EndPhaseCommandTests
{
    [Fact]
    public void Execute_rejects_leaving_reinforce_while_the_actor_still_has_unplaced_troops()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildReinforceReadyState(actor, other, troopsRemaining: 3);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(actor));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.ReinforcementIncomplete, rejection.Error.Code);
    }

    [Fact]
    public void Execute_advances_from_reinforce_to_attack_once_all_reinforcement_troops_are_placed()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildReinforceReadyState(actor, other, troopsRemaining: 0);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(actor));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(TurnPhase.Attack, ok.State.Turn.Phase);
        Assert.Equal(actor, ok.State.Turn.CurrentPlayer);
    }

    [Fact]
    public void Execute_advances_from_reinforce_to_attack_for_the_same_player_via_the_full_setup_flow()
    {
        // GameStateBuilder.CompleteSetup places starting troops AND the
        // resulting first-Reinforce-phase reinforcement, so the returned
        // state is already fully placed and ready to end the phase.
        var state = GameStateBuilder.CompleteSetup(2);
        var actor = state.Turn.CurrentPlayer;
        Assert.Equal(0, state.Players.Single(p => p.Id == actor).TroopsRemaining);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(actor));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(TurnPhase.Attack, ok.State.Turn.Phase);
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
    public void Execute_does_not_draw_at_Fortify_to_next_player_transition_even_if_ConqueredThisTurn_is_true()
    {
        var actor = new PlayerId(0);
        var next = new PlayerId(1);
        var state = BuildFortifyPhaseState(actor, next, conqueredThisTurn: true, fortifyUsed: false);
        var deckCountBefore = state.Deck.Count;
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(actor));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Empty(ok.Events.OfType<CardDrawn>());
        Assert.Equal(deckCountBefore, ok.State.Deck.Count);
        Assert.Empty(ok.State.Players.Single(p => p.Id == actor).Hand);
    }

    /// <summary>
    /// Regression test for the bug where <c>AdvanceToNextPlayer</c>
    /// absolutely overwrote <c>TroopsRemaining</c> instead of adding to it. A
    /// player can bank a card-trade bonus while <c>TroopsRemaining</c> is
    /// already nonzero: a mandatory overflow trade-down forced mid-Attack by
    /// an elimination (see <c>TurnState.MandatoryTradeDown</c>) leaves the
    /// bonus sitting untouched in <c>PlayerState.TroopsRemaining</c> because
    /// <c>ExecutePlaceTroops</c> rejects placement outside Setup/Reinforce.
    /// That bonus must still be there — ADDED to the freshly computed
    /// reinforcement, not replaced by it — the next time this player's own
    /// Reinforce phase begins, even after a full turn cycle through another
    /// player in between.
    /// </summary>
    [Fact]
    public void Execute_adds_a_banked_out_of_phase_trade_bonus_to_the_traders_next_reinforcement_after_a_full_turn_cycle()
    {
        var actor = new PlayerId(0);
        var next = new PlayerId(1);
        IReadOnlyList<Card> tradedHand =
        [
            new TerritoryCard(new TerritoryId("Alaska"), CardSymbol.Infantry),
            new TerritoryCard(new TerritoryId("Alberta"), CardSymbol.Infantry),
            new TerritoryCard(new TerritoryId("Ontario"), CardSymbol.Infantry)
        ];
        var state = BuildAttackPhaseStateWithTradeableHand(actor, next, tradedHand);
        var engine = new GameEngine(new QueuedDiceRoller());

        // Bank a trade bonus mid-Attack (phase-agnostic per RequiredPhaseFor)
        // — this is the only way TroopsRemaining becomes nonzero outside
        // Reinforce; see the class-level doc comment above.
        var traded = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new TradeCardsCommand(actor, tradedHand)));
        state = traded.State;
        var bankedBonus = state.Players.Single(p => p.Id == actor).TroopsRemaining;
        Assert.Equal(4, bankedBonus); // first trade this game — can't be spent in Attack

        // Actor finishes their own turn: Attack -> Fortify -> next's Reinforce.
        state = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new EndPhaseCommand(actor))).State;
        state = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new EndPhaseCommand(actor))).State;
        Assert.Equal(next, state.Turn.CurrentPlayer);
        Assert.Equal(TurnPhase.Reinforce, state.Turn.Phase);

        // The bonus must survive, untouched, through next's entire turn —
        // AdvanceToNextPlayer only ever assigns the INCOMING player's pool.
        Assert.Equal(bankedBonus, state.Players.Single(p => p.Id == actor).TroopsRemaining);

        // next plays out their whole turn: place all reinforcement, then
        // Reinforce -> Attack -> Fortify -> back to actor's Reinforce.
        var nextTerritory = state.Territories.First(kv => kv.Value.Owner == next).Key;
        var nextReinforcement = state.Players.Single(p => p.Id == next).TroopsRemaining;
        state = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new PlaceTroopsCommand(next, nextTerritory, nextReinforcement))).State;
        state = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new EndPhaseCommand(next))).State;
        state = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new EndPhaseCommand(next))).State;
        Assert.Equal(TurnPhase.Fortify, state.Turn.Phase);

        var expectedReinforcementForActor = Reinforcement.Calculate(state.Territories, actor);

        var result = engine.Execute(state, new EndPhaseCommand(next));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(actor, ok.State.Turn.CurrentPlayer);
        Assert.Equal(TurnPhase.Reinforce, ok.State.Turn.Phase);
        Assert.Equal(
            bankedBonus + expectedReinforcementForActor,
            ok.State.Players.Single(p => p.Id == actor).TroopsRemaining);
    }

    [Fact]
    public void Execute_rejects_end_phase_from_a_player_who_is_not_the_active_player()
    {
        var state = GameStateBuilder.CompleteSetup(2);
        // Exclude the neutral third party (GameSetup.Create's default mode,
        // GameMode.TwoPlayer, deals a neutral army) — this test
        // only cares about the other human, not the board-object neutral.
        var inactivePlayer = state.Players.Single(p => !p.IsNeutral && p.Id != state.Turn.CurrentPlayer).Id;
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(inactivePlayer));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotYourTurn, rejection.Error.Code);
    }

    /// <summary>Builds a minimal 2-player Reinforce-phase state for <paramref name="actor"/> with the given troop pool.</summary>
    private static GameState BuildReinforceReadyState(PlayerId actor, PlayerId other, int troopsRemaining)
    {
        var territories = WorldMap.Territories.ToDictionary(t => t.Id, t => new TerritoryState(actor, 1));

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(actor, [], false, troopsRemaining),
            new PlayerState(other, [], false, 0)
        ];

        return new GameState(
            territories, players, new TurnState(actor, TurnPhase.Reinforce), Deck.CreateStandard(), [], new GameStatus.InProgress());
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

    /// <summary>
    /// Builds a minimal 2-player state where <paramref name="actor"/> is in
    /// the Attack phase holding <paramref name="hand"/> (a valid trade-in set
    /// naming territories owned by <paramref name="next"/>, so trading it
    /// needs no bonus-territory choice) and owns every territory except the 6
    /// North American territories, which belong to <paramref name="next"/> —
    /// same layout as <see cref="BuildFortifyPhaseState"/>.
    /// </summary>
    private static GameState BuildAttackPhaseStateWithTradeableHand(PlayerId actor, PlayerId next, IReadOnlyList<Card> hand)
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
            new PlayerState(actor, hand, false, 0),
            new PlayerState(next, [], false, 0)
        ];

        var turn = new TurnState(actor, TurnPhase.Attack);

        return new GameState(territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress());
    }
}
