using System.Reflection;
using Risk.Domain.Cards;
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
/// Unit-level coverage of design D1: <see cref="GameEngine.Observe"/>'s
/// derived headquarters-reveal predicate
/// (<c>Players.All(p =&gt; p.HeadquartersId is not null)</c>). Hand-builds
/// <see cref="TurnPhase.SelectHeadquarters"/>-shaped states directly (no
/// command execution needed — the predicate reads only
/// <see cref="PlayerState.HeadquartersId"/>), mirroring
/// <see cref="GameEngineObserveTests"/>'s hand-built-state pattern.
/// </summary>
public class HeadquartersVisibilityTests
{
    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId NorthwestTerritory = new("NorthwestTerritory");

    [Fact]
    public void Observe_before_all_players_have_selected_shows_own_headquarters_but_hides_others()
    {
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var state = BuildTwoPlayerCapitalState(aHeadquarters: Alaska, bHeadquarters: null);
        var engine = new GameEngine(new QueuedDiceRoller());

        var viewA = engine.Observe(state, a);
        Assert.Equal(Alaska, viewA.OwnHeadquarters);
        Assert.Empty(viewA.RevealedHeadquarters);

        var viewB = engine.Observe(state, b);
        Assert.Null(viewB.OwnHeadquarters);
        Assert.Empty(viewB.RevealedHeadquarters);
    }

    [Fact]
    public void Observe_after_every_player_has_selected_reveals_every_players_headquarters_to_any_viewer()
    {
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var state = BuildTwoPlayerCapitalState(aHeadquarters: Alaska, bHeadquarters: NorthwestTerritory);
        var engine = new GameEngine(new QueuedDiceRoller());

        var viewA = engine.Observe(state, a);
        Assert.Equal(Alaska, viewA.OwnHeadquarters);
        Assert.Equal(2, viewA.RevealedHeadquarters.Count);
        Assert.Equal(Alaska, viewA.RevealedHeadquarters[a]);
        Assert.Equal(NorthwestTerritory, viewA.RevealedHeadquarters[b]);

        var viewB = engine.Observe(state, b);
        Assert.Equal(NorthwestTerritory, viewB.OwnHeadquarters);
        Assert.Equal(2, viewB.RevealedHeadquarters.Count);
        Assert.Equal(Alaska, viewB.RevealedHeadquarters[a]);
        Assert.Equal(NorthwestTerritory, viewB.RevealedHeadquarters[b]);
    }

    [Theory]
    [InlineData(GameMode.Classic, 2)]
    [InlineData(GameMode.TwoPlayer, 2)]
    [InlineData(GameMode.SecretMission, 2)]
    public void Observe_never_populates_headquarters_fields_for_non_Capital_modes(GameMode mode, int playerCount)
    {
        // Regression boundary: non-Capital games never set HeadquartersId on
        // any PlayerState, so the reveal predicate is vacuously true (an
        // empty/no-HQ player set trivially satisfies All(...)) but every
        // OwnHeadquarters/RevealedHeadquarters value stays null/empty because
        // there is no non-null TerritoryId to expose in the first place.
        var state = BuildTwoPlayerCapitalState(aHeadquarters: null, bHeadquarters: null) with { Mode = mode };
        var engine = new GameEngine(new QueuedDiceRoller());

        for (var i = 0; i < playerCount; i++)
        {
            var view = engine.Observe(state, new PlayerId(i));
            Assert.Null(view.OwnHeadquarters);
            Assert.Empty(view.RevealedHeadquarters);
        }
    }

    [Fact]
    public void HeadquartersSelected_event_carries_no_territory_field()
    {
        // Guards the PR2 secrecy fix (design D1): GameState.Log is public and
        // unredacted, so HeadquartersSelected must never carry a TerritoryId
        // or any other territory-shaped payload. This test would FAIL if
        // someone regressed the event back to (PlayerId Player, TerritoryId
        // Territory) — the property count/type check below inspects the
        // compiled type, not just current call sites.
        var properties = typeof(HeadquartersSelected).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var property = Assert.Single(properties);
        Assert.Equal(nameof(HeadquartersSelected.Player), property.Name);
        Assert.Equal(typeof(PlayerId), property.PropertyType);
    }

    private static GameState BuildTwoPlayerCapitalState(TerritoryId? aHeadquarters, TerritoryId? bHeadquarters)
    {
        var a = new PlayerId(0);
        var b = new PlayerId(1);

        var territories = new Dictionary<TerritoryId, TerritoryState>
        {
            [Alaska] = new TerritoryState(a, 3),
            [NorthwestTerritory] = new TerritoryState(b, 3)
        };

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(a, [], false, 0, HeadquartersId: aHeadquarters),
            new PlayerState(b, [], false, 0, HeadquartersId: bHeadquarters)
        ];

        var turn = new TurnState(a, TurnPhase.SelectHeadquarters);

        return new GameState(territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress(), Mode: GameMode.Capital);
    }
}

/// <summary>
/// Full-game integration test (task 3.3): drives a real Capital-mode game
/// through Claim -&gt; Setup -&gt; SelectHeadquarters, asserting reveal timing via
/// <see cref="GameEngine.Observe"/> at each step, plus the structural
/// card-exclusion mechanism (deck removal, never entering any hand).
/// Mirrors <c>SelectHeadquartersRoundRobinTests</c>'s real-map driving style.
/// </summary>
public class HeadquartersVisibilityIntegrationTests
{
    [Fact]
    public void Capital_game_hides_headquarters_until_the_last_selection_then_reveals_to_every_viewer()
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

        var initialDeckCount = state.Deck.Count;
        var selectedByPlayer = new Dictionary<PlayerId, TerritoryId>();

        // First two of three selections: reveal must stay closed for every
        // viewer, even the players who have already selected.
        for (var i = 0; i < 2; i++)
        {
            var actor = state.Turn.CurrentPlayer;
            var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
            selectedByPlayer[actor] = territory;

            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new SelectHeadquartersCommand(actor, territory)));
            state = result.State;

            foreach (var viewerId in new[] { new PlayerId(0), new PlayerId(1), new PlayerId(2) })
            {
                var view = engine.Observe(state, viewerId);
                Assert.Empty(view.RevealedHeadquarters);
                Assert.Equal(
                    selectedByPlayer.TryGetValue(viewerId, out var own) ? own : (TerritoryId?)null,
                    view.OwnHeadquarters);
            }
        }

        // Final (3rd) selection: transitions to Reinforce and reveals every
        // player's headquarters to every viewer.
        var lastActor = state.Turn.CurrentPlayer;
        var lastTerritory = state.Territories.First(kv => kv.Value.Owner == lastActor).Key;
        selectedByPlayer[lastActor] = lastTerritory;

        var finalResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new SelectHeadquartersCommand(lastActor, lastTerritory)));
        state = finalResult.State;

        Assert.Equal(TurnPhase.Reinforce, state.Turn.Phase);

        foreach (var viewerId in new[] { new PlayerId(0), new PlayerId(1), new PlayerId(2) })
        {
            var view = engine.Observe(state, viewerId);
            Assert.Equal(3, view.RevealedHeadquarters.Count);
            foreach (var (playerId, territory) in selectedByPlayer)
            {
                Assert.Equal(territory, view.RevealedHeadquarters[playerId]);
            }
            Assert.Equal(selectedByPlayer[viewerId], view.OwnHeadquarters);
        }

        // Raw event log stays leak-free: every HeadquartersSelected entry
        // carries only the actor, never the territory.
        var selectedEvents = state.Log.OfType<HeadquartersSelected>().ToList();
        Assert.Equal(3, selectedEvents.Count);
        Assert.Equal(selectedByPlayer.Keys.OrderBy(p => p.Value), selectedEvents.Select(e => e.Player).OrderBy(p => p.Value));

        var revealedEvent = Assert.Single(state.Log.OfType<HeadquartersRevealed>());
        Assert.Equal(3, revealedEvent.Headquarters.Count);

        // Card-exclusion mechanism: every selected territory's TerritoryCard
        // is gone from the deck and never present in the selecting player's
        // hand.
        Assert.Equal(initialDeckCount - 3, state.Deck.Count);
        foreach (var (playerId, territory) in selectedByPlayer)
        {
            Assert.DoesNotContain(state.Deck, c => c is TerritoryCard tc && tc.Territory == territory);
            var hand = state.Players.Single(p => p.Id == playerId).Hand;
            Assert.DoesNotContain(hand, c => c is TerritoryCard tc && tc.Territory == territory);
        }
    }
}
