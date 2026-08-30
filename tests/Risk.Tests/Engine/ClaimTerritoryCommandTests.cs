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
/// <see cref="ClaimTerritoryCommand"/> unit tests hand-build minimal
/// <see cref="TurnPhase.Claim"/> states and call <see cref="GameEngine.Execute"/>
/// directly. Item 2.1/PR3 reverses design decision D4 ("claiming never
/// advances the turn or phase", pinned in item 1.3): claiming now rotates to
/// the next player and, on the final claim, transitions
/// <see cref="TurnPhase.Claim"/> → <see cref="TurnPhase.Setup"/> — see
/// <see cref="Execute_claims_an_unowned_territory_and_rotates_to_the_next_player_while_territories_remain"/>
/// and <see cref="Execute_the_final_claim_transitions_Claim_to_Setup_at_the_rotated_player"/>
/// below, which replace the old "Turn never changes" assertion.
/// </summary>
public class ClaimTerritoryCommandTests
{
    private static readonly TerritoryId Alaska = new("Alaska"); // unclaimed in the test fixture
    private static readonly TerritoryId Kamchatka = new("Kamchatka"); // unclaimed in the rotation fixture
    private static readonly TerritoryId NorthwestTerritory = new("NorthwestTerritory"); // already owned by "other"

    [Fact]
    public void Execute_claims_an_unowned_territory_and_decrements_the_actors_troop_pool()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseStateWithTwoUnclaimed(actor, other, actorTroopsRemaining: 5);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, Alaska, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var claimed = ok.State.Territories[Alaska];
        Assert.Equal(actor, claimed.Owner);
        Assert.Equal(1, claimed.Troops);
        Assert.Equal(4, ok.State.Players.Single(p => p.Id == actor).TroopsRemaining);

        var claimedEvent = Assert.IsType<TerritoryClaimed>(Assert.Single(ok.Events));
        Assert.Equal(actor, claimedEvent.Player);
        Assert.Equal(Alaska, claimedEvent.Territory);
        Assert.Equal(1, claimedEvent.Troops);
    }

    [Fact]
    public void Execute_claims_an_unowned_territory_and_rotates_to_the_next_player_while_territories_remain()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseStateWithTwoUnclaimed(actor, other, actorTroopsRemaining: 5);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, Alaska, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);

        // D4 reversal (item 2.1/PR3): claiming now rotates to the next
        // player while territories remain unclaimed (Kamchatka is still
        // unowned here); the phase stays Claim. Only the final claim
        // transitions to Setup — see the completion test below.
        Assert.Equal(TurnPhase.Claim, ok.State.Turn.Phase);
        Assert.Equal(other, ok.State.Turn.CurrentPlayer);
        Assert.DoesNotContain(ok.Events, e => e is PhaseChanged);
    }

    [Fact]
    public void Execute_the_final_claim_transitions_Claim_to_Setup_at_the_rotated_player()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseState(actor, other, actorTroopsRemaining: 5);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, Alaska, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.All(ok.State.Territories.Values, t => Assert.NotNull(t.Owner));

        // The rotated player (other) takes over Setup — not the claimer, and
        // not a reset to players[0] (here that would coincidentally also be
        // "other" is false: players[0] is "actor", so a buggy reset would
        // wrongly leave CurrentPlayer == actor instead of rotating to other).
        Assert.Equal(TurnPhase.Setup, ok.State.Turn.Phase);
        Assert.Equal(other, ok.State.Turn.CurrentPlayer);

        var phaseChanged = Assert.Single(ok.Events.OfType<PhaseChanged>());
        Assert.Equal(TurnPhase.Claim, phaseChanged.From);
        Assert.Equal(TurnPhase.Setup, phaseChanged.To);
        Assert.Equal(other, phaseChanged.CurrentPlayer);
    }

    [Fact]
    public void Execute_rejects_a_claim_with_more_than_one_troop()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseStateWithTwoUnclaimed(actor, other, actorTroopsRemaining: 5);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, Alaska, 2));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidTroopCount, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_claiming_an_already_owned_territory()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseState(actor, other, actorTroopsRemaining: 5);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, NorthwestTerritory, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotOwner, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_an_unknown_territory()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseState(actor, other, actorTroopsRemaining: 5);
        var engine = new GameEngine(new QueuedDiceRoller());
        var unknown = new TerritoryId("NotOnTheMap");

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, unknown, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotOwner, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_a_troop_count_below_one()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseState(actor, other, actorTroopsRemaining: 5);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, Alaska, 0));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidTroopCount, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_a_troop_count_above_the_actors_remaining_pool()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseState(actor, other, actorTroopsRemaining: 2);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, Alaska, 3));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidTroopCount, rejection.Error.Code);
    }

    [Theory]
    [InlineData(TurnPhase.Setup)]
    [InlineData(TurnPhase.Reinforce)]
    [InlineData(TurnPhase.Attack)]
    [InlineData(TurnPhase.Fortify)]
    public void Execute_rejects_claiming_in_every_non_claim_phase(TurnPhase phase)
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseState(actor, other, actorTroopsRemaining: 5) with
        {
            Turn = new TurnState(actor, phase)
        };
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, Alaska, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.WrongPhase, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_a_claim_from_a_player_who_is_not_the_current_player()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseState(actor, other, actorTroopsRemaining: 5);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(other, Alaska, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotYourTurn, rejection.Error.Code);
    }

    /// <summary>
    /// Minimal hand-built <see cref="TurnPhase.Claim"/> state: <see cref="Alaska"/>
    /// is unclaimed (<c>Owner == null</c>), <see cref="NorthwestTerritory"/> is
    /// already owned by <paramref name="other"/>. No built-in flow produces this
    /// shape yet — see the class doc.
    /// </summary>
    private static GameState BuildClaimPhaseState(PlayerId currentPlayer, PlayerId other, int actorTroopsRemaining)
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>
        {
            [Alaska] = new TerritoryState(null, 0),
            [NorthwestTerritory] = new TerritoryState(other, 1)
        };

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(currentPlayer, [], false, actorTroopsRemaining),
            new PlayerState(other, [], false, 0)
        ];

        var turn = new TurnState(currentPlayer, TurnPhase.Claim);

        return new GameState(territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress());
    }

    /// <summary>
    /// Same shape as <see cref="BuildClaimPhaseState"/> but with a second
    /// unclaimed territory (<see cref="Kamchatka"/>), so a single claim on
    /// <see cref="Alaska"/> leaves the Claim phase incomplete — used by tests
    /// that assert per-claim rotation without triggering the Claim → Setup
    /// completion transition.
    /// </summary>
    private static GameState BuildClaimPhaseStateWithTwoUnclaimed(PlayerId currentPlayer, PlayerId other, int actorTroopsRemaining)
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>
        {
            [Alaska] = new TerritoryState(null, 0),
            [Kamchatka] = new TerritoryState(null, 0),
            [NorthwestTerritory] = new TerritoryState(other, 1)
        };

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(currentPlayer, [], false, actorTroopsRemaining),
            new PlayerState(other, [], false, 0)
        ];

        var turn = new TurnState(currentPlayer, TurnPhase.Claim);

        return new GameState(territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress());
    }
}

/// <summary>
/// Integration-style tests that drive a real <see cref="GameSetup.Create"/>
/// Classic-mode game through the full Claim phase (round-robin, via
/// <see cref="GameStateBuilder.CompleteClaimPhase"/>) and beyond — proving
/// the rotation/transition logic wired in item 2.1/PR3 against the real
/// 42-territory map instead of the hand-built minimal fixtures above.
/// </summary>
public class ClaimPhaseRoundRobinTests
{
    [Fact]
    public void Full_Claim_phase_with_5_players_divides_42_territories_unevenly_and_transitions_without_resetting_to_the_first_seat()
    {
        var setupOk = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(5, GameMode.Classic, QueuedDiceRoller.ForRollOff(5)));
        var state = setupOk.State;
        var engine = new GameEngine(new QueuedDiceRoller());

        var claimCounts = Enumerable.Range(0, 5).ToDictionary(i => new PlayerId(i), _ => 0);
        var lastClaimer = state.Turn.CurrentPlayer;

        while (state.Turn.Phase == TurnPhase.Claim)
        {
            var actor = state.Turn.CurrentPlayer;
            lastClaimer = actor;
            var territory = state.Territories.First(kv => kv.Value.Owner is null).Key;

            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new ClaimTerritoryCommand(actor, territory, 1)));
            claimCounts[actor]++;
            state = result.State;
        }

        // 42 is not divisible by 5: rotation starts at the roll-off winner
        // (player 0, via ForRollOff), so seats 0-1 claim 9 territories each
        // and seats 2-4 claim 8 each (2 * 9 + 3 * 8 = 42).
        Assert.Equal(9, claimCounts[new PlayerId(0)]);
        Assert.Equal(9, claimCounts[new PlayerId(1)]);
        Assert.Equal(8, claimCounts[new PlayerId(2)]);
        Assert.Equal(8, claimCounts[new PlayerId(3)]);
        Assert.Equal(8, claimCounts[new PlayerId(4)]);
        Assert.Equal(42, claimCounts.Values.Sum());

        // Rotation-position continuity: the 42nd claim transitions
        // Claim -> Setup at the ROTATED next player — not the 42nd claimer,
        // and not reset to players[0].
        Assert.Equal(TurnPhase.Setup, state.Turn.Phase);
        var expectedNext = new PlayerId((lastClaimer.Value + 1) % 5);
        Assert.Equal(expectedNext, state.Turn.CurrentPlayer);
        Assert.NotEqual(lastClaimer, state.Turn.CurrentPlayer);
    }

    [Theory]
    [InlineData(3, 35)]
    [InlineData(4, 30)]
    [InlineData(5, 25)]
    public void Full_Claim_phase_conserves_every_players_troop_pool_and_the_Setup_placement_loop_reaches_Reinforce(int playerCount, int startingTroops)
    {
        var setupOk = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(playerCount, GameMode.Classic, QueuedDiceRoller.ForRollOff(playerCount)));
        var engine = new GameEngine(new QueuedDiceRoller());

        var claimedState = GameStateBuilder.CompleteClaimPhase(setupOk.State, engine);

        Assert.Equal(TurnPhase.Setup, claimedState.Turn.Phase);
        Assert.All(claimedState.Territories.Values, t => Assert.NotNull(t.Owner));

        // Troop conservation (design D6/Q4): every troop placed during Claim
        // plus every troop still in a player's pool must equal the official
        // starting pool — no double-counting, no leak.
        var troopsOnBoard = claimedState.Territories.Values.Sum(t => t.Troops);
        var troopsRemaining = claimedState.Players.Sum(p => p.TroopsRemaining);
        Assert.Equal(playerCount * startingTroops, troopsOnBoard + troopsRemaining);

        // No dead end: the existing Setup placement loop carries the game
        // all the way through to Reinforce.
        var reinforceState = GameStateBuilder.CompleteSetup(playerCount, GameMode.Classic);
        Assert.Equal(TurnPhase.Reinforce, reinforceState.Turn.Phase);
    }
}

/// <summary>
/// The single most important test in item 2.1/PR3: proves there is no dead
/// end anywhere in the Classic-mode lifecycle by driving a real 3-player
/// game — via the public <see cref="IGameEngine.Execute"/> command pipeline
/// only — from <see cref="GameSetup.Create"/>'s Claim-phase start, through
/// the full 42-territory round-robin claim, through Setup's troop-placement
/// loop, into the normal Reinforce/Attack/Fortify turn cycle, to a
/// <see cref="GameStatus.Won"/> state resolved by
/// <see cref="Risk.Engine.Modes.ConquestVictoryRule"/>. Mirrors
/// <c>FullGameIntegrationTests</c>' 2-player script (item 1.7/PR8), adapted
/// for a 3-player Classic game and duplicated here — rather than reusing
/// that class's private helpers — to keep this PR's diff scoped to
/// <c>ClaimTerritoryCommandTests.cs</c>/<c>GameStateBuilder.cs</c>/<c>GameEngine.cs</c>.
/// </summary>
public class ClaimThroughVictoryFullGameIntegrationTests
{
    private const int MaxCommands = 5_000; // safety net: fail loudly instead of hanging if conquest ever stalls

    [Fact]
    public void A_full_Classic_mode_game_reaches_victory_from_Claim_through_ConquestVictoryRule()
    {
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var setupOk = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(3, GameMode.Classic, QueuedDiceRoller.ForRollOff(3)));
        var state = setupOk.State;
        var attacker = state.Turn.CurrentPlayer; // roll-off winner, always player 0 via ForRollOff

        Assert.Equal(TurnPhase.Claim, state.Turn.Phase);

        // Claim phase: round-robin claim every territory until the map is
        // full — proving ClaimTerritoryCommand is no longer a dead end.
        state = GameStateBuilder.CompleteClaimPhase(state, engine);
        Assert.Equal(TurnPhase.Setup, state.Turn.Phase);
        Assert.All(state.Territories.Values, t => Assert.NotNull(t.Owner));

        // Setup phase: place every remaining starting troop, one at a time, in rotation.
        while (state.Turn.Phase == TurnPhase.Setup)
        {
            state = GameSimulation.PlaceOneStartingTroop(engine, state);
        }

        var commandsIssued = 0;
        while (state.Status is not GameStatus.Won)
        {
            Assert.True(++commandsIssued < MaxCommands, "Full Classic-mode lifecycle script exceeded the safety command cap without reaching victory.");

            var actor = state.Turn.CurrentPlayer;

            if (state.Turn.Phase == TurnPhase.Reinforce)
            {
                if (actor == attacker)
                {
                    state = GameSimulation.TradeAllAvailableSets(engine, state, actor);
                    state = GameSimulation.PlaceReinforcementOnStrongestTerritory(engine, state);
                }
                else
                {
                    state = GameStateBuilder.PlaceAllReinforcementTroops(state, engine);
                }

                state = GameSimulation.EndPhase(engine, state, actor);
                continue;
            }

            if (state.Turn.Phase == TurnPhase.Attack)
            {
                if (actor == attacker && GameSimulation.TryFindAttack(state, actor, out var from, out var to))
                {
                    state = GameSimulation.AttackAndOccupy(engine, state, actor, from, to);
                    continue;
                }

                state = GameSimulation.EndPhase(engine, state, actor);
                continue;
            }

            if (state.Turn.Phase == TurnPhase.Fortify)
            {
                if (actor == attacker)
                {
                    state = GameSimulation.TryFortifyTowardAFrontier(engine, actor, state);
                }

                state = GameSimulation.EndPhase(engine, state, actor);
                continue;
            }
        }

        var won = Assert.IsType<GameStatus.Won>(state.Status);
        Assert.Equal(attacker, won.Winner);
        Assert.All(state.Territories.Values, t => Assert.Equal(attacker, t.Owner));
        Assert.Equal(WorldMap.Territories.Count, state.Territories.Count(t => t.Value.Owner == attacker));
        Assert.Contains(state.Log, e => e is GameWon);

        // Once won, the engine must refuse any further command — proving
        // the lifecycle has a clean end, not just a reachable middle.
        var rejected = engine.Execute(state, new EndPhaseCommand(attacker));
        Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(rejected);
    }

}
