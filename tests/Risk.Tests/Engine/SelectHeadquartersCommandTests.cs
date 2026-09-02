using Risk.Domain.Cards;
using Risk.Domain.Errors;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Setup;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

/// <summary>
/// <see cref="SelectHeadquartersCommand"/> unit tests hand-build minimal
/// <see cref="TurnPhase.SelectHeadquarters"/> states and call
/// <see cref="GameEngine.Execute"/> directly, mirroring
/// <c>ClaimTerritoryCommandTests</c>'s hand-built-state pattern. Ownership is
/// the only constraint (design D2/spec) — no continent or adjacency rule
/// applies.
/// </summary>
public class SelectHeadquartersCommandTests
{
    private static readonly TerritoryId Alaska = new("Alaska"); // owned by the acting player
    private static readonly TerritoryId NorthwestTerritory = new("NorthwestTerritory"); // owned by the other player

    [Fact]
    public void Execute_selects_an_owned_territory_as_headquarters()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildSelectHeadquartersPhaseState(actor, other);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new SelectHeadquartersCommand(actor, Alaska));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(Alaska, ok.State.Players.Single(p => p.Id == actor).HeadquartersId);

        // Design D1: the per-selection event is territory-free — GameState.Log
        // is public/unredacted, so the territory must not appear here.
        var selectedEvent = Assert.IsType<HeadquartersSelected>(Assert.Single(ok.Events));
        Assert.Equal(actor, selectedEvent.Player);
    }

    [Fact]
    public void Execute_removes_the_selected_territorys_card_from_the_deck_and_leaves_wildcards_intact()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildSelectHeadquartersPhaseState(actor, other);
        var initialDeckCount = state.Deck.Count;
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new SelectHeadquartersCommand(actor, Alaska));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(initialDeckCount - 1, ok.State.Deck.Count);
        Assert.DoesNotContain(ok.State.Deck, c => c is TerritoryCard tc && tc.Territory == Alaska);
        Assert.Equal(2, ok.State.Deck.OfType<WildCard>().Count());
    }

    [Fact]
    public void Execute_rejects_selecting_a_territory_owned_by_another_player()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildSelectHeadquartersPhaseState(actor, other);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new SelectHeadquartersCommand(actor, NorthwestTerritory));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotOwner, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_selecting_an_unknown_territory()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildSelectHeadquartersPhaseState(actor, other);
        var engine = new GameEngine(new QueuedDiceRoller());
        var unknown = new TerritoryId("NotOnTheMap");

        var result = engine.Execute(state, new SelectHeadquartersCommand(actor, unknown));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotOwner, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_a_selection_from_a_player_who_is_not_the_current_player()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildSelectHeadquartersPhaseState(actor, other);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new SelectHeadquartersCommand(other, NorthwestTerritory));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotYourTurn, rejection.Error.Code);
    }

    [Theory]
    [InlineData(TurnPhase.Claim)]
    [InlineData(TurnPhase.Setup)]
    [InlineData(TurnPhase.Reinforce)]
    [InlineData(TurnPhase.Attack)]
    [InlineData(TurnPhase.Fortify)]
    public void Execute_rejects_selecting_headquarters_in_every_non_selectheadquarters_phase(TurnPhase phase)
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildSelectHeadquartersPhaseState(actor, other) with
        {
            Turn = new TurnState(actor, phase)
        };
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new SelectHeadquartersCommand(actor, Alaska));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.WrongPhase, rejection.Error.Code);
    }

    [Fact]
    public void AttackCommand_during_SelectHeadquarters_is_rejected_with_WrongPhase()
    {
        // Pins design D3's unreachability proof: elimination cannot occur
        // before SelectHeadquarters completes because Attack requires
        // TurnPhase.Attack, which this phase gate refuses to reach —
        // exercised here directly, not merely asserted about the reveal
        // predicate's lack of an IsEliminated check.
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildSelectHeadquartersPhaseState(actor, other);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new AttackCommand(actor, Alaska, NorthwestTerritory, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.WrongPhase, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_a_repeated_selection_from_the_same_player_with_NotYourTurn_then_WrongPhase_after_completion()
    {
        // Design D2: no dedicated double-selection guard exists — a
        // re-selection before the actor's next turn hits the ordinary
        // NotYourTurn gate (proven unreachable-otherwise by construction:
        // the handler rotates the turn on every accepted selection).
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildSelectHeadquartersPhaseState(actor, other);
        var engine = new GameEngine(new QueuedDiceRoller());

        var first = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new SelectHeadquartersCommand(actor, Alaska)));
        state = first.State;

        var repeated = engine.Execute(state, new SelectHeadquartersCommand(actor, Alaska));
        var repeatedRejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(repeated);
        Assert.Equal(GameErrorCode.NotYourTurn, repeatedRejection.Error.Code);

        var second = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new SelectHeadquartersCommand(other, NorthwestTerritory)));
        state = second.State;

        // Both players have now selected: the phase has advanced to
        // Reinforce at players[0] (== actor here), so a further selection
        // attempt from actor hits the phase gate, not NotYourTurn — proving
        // this is a genuine WrongPhase rejection, not a coincidental repeat
        // of the earlier NotYourTurn path.
        Assert.Equal(TurnPhase.Reinforce, state.Turn.Phase);
        Assert.Equal(actor, state.Turn.CurrentPlayer);

        var afterCompletion = engine.Execute(state, new SelectHeadquartersCommand(actor, Alaska));
        var afterCompletionRejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(afterCompletion);
        Assert.Equal(GameErrorCode.WrongPhase, afterCompletionRejection.Error.Code);
    }

    /// <summary>
    /// Minimal hand-built <see cref="TurnPhase.SelectHeadquarters"/> state:
    /// <see cref="Alaska"/> is owned by <paramref name="currentPlayer"/>,
    /// <see cref="NorthwestTerritory"/> is owned by <paramref name="other"/>.
    /// Mode is <see cref="GameMode.Capital"/> since this phase is only ever
    /// reachable there.
    /// </summary>
    private static GameState BuildSelectHeadquartersPhaseState(PlayerId currentPlayer, PlayerId other)
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>
        {
            [Alaska] = new TerritoryState(currentPlayer, 3),
            [NorthwestTerritory] = new TerritoryState(other, 3)
        };

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(currentPlayer, [], false, 0),
            new PlayerState(other, [], false, 0)
        ];

        var turn = new TurnState(currentPlayer, TurnPhase.SelectHeadquarters);

        return new GameState(territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress(), Mode: GameMode.Capital);
    }
}

/// <summary>
/// Integration-style tests that drive a real <see cref="GameSetup.Create"/>
/// Capital-mode game through Claim, Setup, and the full
/// <see cref="TurnPhase.SelectHeadquarters"/> round — proving rotation, deck
/// removal, and the completion transition against the real 42-territory map,
/// mirroring <c>ClaimPhaseRoundRobinTests</c>.
/// </summary>
public class SelectHeadquartersRoundRobinTests
{
    [Fact]
    public void Full_SelectHeadquarters_round_rotates_players_removes_deck_cards_and_transitions_to_Reinforce_at_players_zero()
    {
        var setupOk = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(3, GameMode.Capital, QueuedDiceRoller.ForRollOff(3)));
        var state = setupOk.State;
        var engine = new GameEngine(new QueuedDiceRoller());

        state = GameStateBuilder.CompleteClaimPhase(state, engine);
        while (state.Players.Any(p => p.TroopsRemaining > 0))
        {
            var setupActor = state.Turn.CurrentPlayer;
            var setupTerritory = state.Territories.First(kv => kv.Value.Owner == setupActor).Key;
            var setupResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new PlaceTroopsCommand(setupActor, setupTerritory, 1)));
            state = setupResult.State;
        }

        Assert.Equal(TurnPhase.SelectHeadquarters, state.Turn.Phase);
        Assert.Equal(new PlayerId(0), state.Turn.CurrentPlayer);

        var initialDeckCount = state.Deck.Count;
        var selectedTerritories = new List<TerritoryId>();

        // First two selections: phase stays SelectHeadquarters, rotating
        // to the next player each time, and the reveal event does not fire.
        for (var i = 0; i < 2; i++)
        {
            var actor = state.Turn.CurrentPlayer;
            var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
            selectedTerritories.Add(territory);

            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new SelectHeadquartersCommand(actor, territory)));
            state = result.State;

            Assert.Equal(TurnPhase.SelectHeadquarters, state.Turn.Phase);
            Assert.Equal(new PlayerId((actor.Value + 1) % 3), state.Turn.CurrentPlayer);
            Assert.DoesNotContain(result.Events, e => e is HeadquartersRevealed);
            var selectedEvent = Assert.IsType<HeadquartersSelected>(Assert.Single(result.Events));
            Assert.Equal(actor, selectedEvent.Player);
        }

        // Final (3rd) selection: transitions to Reinforce at players[0],
        // with that player's reinforcement pool computed.
        var lastActor = state.Turn.CurrentPlayer;
        var lastTerritory = state.Territories.First(kv => kv.Value.Owner == lastActor).Key;
        selectedTerritories.Add(lastTerritory);

        var finalResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new SelectHeadquartersCommand(lastActor, lastTerritory)));
        state = finalResult.State;

        Assert.Equal(TurnPhase.Reinforce, state.Turn.Phase);
        Assert.Equal(new PlayerId(0), state.Turn.CurrentPlayer);
        Assert.All(state.Players, p => Assert.NotNull(p.HeadquartersId));
        Assert.True(state.Players.Single(p => p.Id == new PlayerId(0)).TroopsRemaining > 0);
        Assert.Contains(finalResult.Events, e => e is PhaseChanged pc && pc.From == TurnPhase.SelectHeadquarters && pc.To == TurnPhase.Reinforce && pc.CurrentPlayer == new PlayerId(0));

        var revealed = Assert.IsType<HeadquartersRevealed>(Assert.Single(finalResult.Events, e => e is HeadquartersRevealed));
        Assert.Equal(3, revealed.Headquarters.Count);
        foreach (var territory in selectedTerritories)
        {
            Assert.Contains(revealed.Headquarters.Values, t => t == territory);
        }

        // Deck: exactly 3 TerritoryCards removed (one per selection), the
        // exact selected territories' cards gone, wildcards intact, and none
        // of the selected territories' cards ever entered any player's Hand.
        Assert.Equal(initialDeckCount - 3, state.Deck.Count);
        Assert.Equal(2, state.Deck.OfType<WildCard>().Count());
        foreach (var territory in selectedTerritories)
        {
            Assert.DoesNotContain(state.Deck, c => c is TerritoryCard tc && tc.Territory == territory);
        }
        Assert.All(state.Players, p => Assert.DoesNotContain(p.Hand, c => c is TerritoryCard tc && selectedTerritories.Contains(tc.Territory)));
    }

    [Theory]
    [InlineData(GameMode.Classic, 3)]
    [InlineData(GameMode.TwoPlayer, 2)]
    [InlineData(GameMode.SecretMission, 3)]
    public void Non_Capital_modes_transition_straight_to_Reinforce_after_Setup_completes_never_entering_SelectHeadquarters(GameMode mode, int playerCount)
    {
        // Regression boundary (spec): only GameMode.Capital enters
        // SelectHeadquarters; every other mode's AdvanceAfterSetupPlacement
        // behavior is unchanged by this PR.
        var state = GameStateBuilder.CompleteSetup(playerCount, mode);

        Assert.Equal(TurnPhase.Reinforce, state.Turn.Phase);
        Assert.All(state.Players, p => Assert.Null(p.HeadquartersId));
    }

    [Fact]
    public void Capital_mode_Setup_completion_transitions_to_SelectHeadquarters_not_Reinforce()
    {
        var setupOk = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(3, GameMode.Capital, QueuedDiceRoller.ForRollOff(3)));
        var state = setupOk.State;
        var engine = new GameEngine(new QueuedDiceRoller());

        state = GameStateBuilder.CompleteClaimPhase(state, engine);
        while (state.Players.Any(p => p.TroopsRemaining > 0))
        {
            var actor = state.Turn.CurrentPlayer;
            var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new PlaceTroopsCommand(actor, territory, 1)));
            state = result.State;
        }

        Assert.Equal(TurnPhase.SelectHeadquarters, state.Turn.Phase);
        Assert.All(state.Players, p => Assert.Equal(0, p.TroopsRemaining));
        Assert.All(state.Players, p => Assert.Null(p.HeadquartersId));
    }
}
