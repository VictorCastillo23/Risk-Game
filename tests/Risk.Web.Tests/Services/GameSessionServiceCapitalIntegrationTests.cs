using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Web.Models;
using Risk.Web.Services;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Services;

/// <summary>
/// End-to-end coverage of the Capital-mode web surfaces (roadmap 5.4)
/// through <see cref="GameSessionService"/>, mirroring
/// <see cref="GameSessionServiceFullGameIntegrationTests"/>'s dispatch-only
/// style. Covers the critical constraint of this change: <see cref="HeadquartersTray.IsRevealed"/>
/// MUST be false for every intermediate step of the pick phase, not just
/// before anyone has picked.
/// </summary>
public class GameSessionServiceCapitalIntegrationTests
{
    private static readonly PlayerId PlayerA = new(0);
    private static readonly PlayerId PlayerB = new(1);
    private static readonly PlayerId PlayerC = new(2);
    private static readonly TerritoryId NorthwestTerritory = new("NorthwestTerritory");
    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId Alberta = new("Alberta");

    [Fact]
    public void Capital_game_stays_secret_through_every_intermediate_pick_then_reveals_and_supports_capture_and_recapture()
    {
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var session = new GameSessionService(engine, QueuedDiceRoller.ForRollOff(3));

        var rows = new List<PlayerSetupRow>
        {
            new("Ana", "#E53935", false),
            new("Beto", "#1E88E5", false),
            new("Caro", "#43A047", false)
        };
        var startResult = session.Start(rows, GameMode.Capital);
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(startResult);

        // Player A claims NorthwestTerritory (their future attacking staging
        // ground); Player B claims Alaska (their future HQ) and Alberta
        // (their future recapture staging ground) — both adjacent to Alaska
        // on the real map (WorldMap.cs), so the later attacks are legal.
        var forcedClaims = new Dictionary<PlayerId, Queue<TerritoryId>>
        {
            [PlayerA] = new(new[] { NorthwestTerritory }),
            [PlayerB] = new(new[] { Alaska, Alberta })
        };
        DriveClaimPhase(session, forcedClaims);

        // Concentrate every Setup-phase troop A places onto NorthwestTerritory
        // and every one B places onto Alberta, so both are overwhelming
        // attacking forces later. C's troops go wherever (unused in the
        // attack script, but C still needs a declared HQ for the 3-row
        // assertion below).
        var reinforceTarget = new Dictionary<PlayerId, TerritoryId> { [PlayerA] = NorthwestTerritory, [PlayerB] = Alberta };
        DriveSetupPhase(session, reinforceTarget);

        Assert.Equal(TurnPhase.SelectHeadquarters, session.State!.Turn.Phase);

        // B always designates Alaska; A and C designate whatever territory
        // they own first in enumeration order — recorded here so the tray
        // assertions below can check against the actual picks.
        var forcedHeadquarters = new Dictionary<PlayerId, TerritoryId> { [PlayerB] = Alaska };
        var declaredHeadquarters = DriveHeadquartersSelectionAssertingSecrecyThroughout(session, engine, forcedHeadquarters);

        Assert.Equal(TurnPhase.Reinforce, session.State!.Turn.Phase);
        Assert.Equal(PlayerA, session.State!.Turn.CurrentPlayer);

        foreach (var viewerId in new[] { PlayerA, PlayerB, PlayerC })
        {
            var view = engine.Observe(session.State!, viewerId);
            Assert.True(HeadquartersTray.IsRevealed(view));
        }

        var rowsAfterReveal = HeadquartersTray.Rows(engine.Observe(session.State!, PlayerA));
        Assert.Equal(3, rowsAfterReveal.Count);
        foreach (var (player, territory) in declaredHeadquarters)
        {
            var row = Assert.Single(rowsAfterReveal, r => r.Declarer == player);
            Assert.Equal(territory, row.Territory);
            Assert.Equal(player, row.Holder); // nobody captured yet
        }

        Assert.False(HeadquartersTray.HasLostOwnHeadquarters(engine.Observe(session.State!, PlayerB), PlayerB));

        // --- Player A's turn: Reinforce -> Attack, conquer B's HQ (Alaska) ---
        PlaceAllReinforcementTroopsOn(session, NorthwestTerritory);
        EndPhase(session, PlayerA);
        Assert.Equal(TurnPhase.Attack, session.State!.Turn.Phase);

        var attackAEvents = AttackAndOccupy(session, PlayerA, NorthwestTerritory, Alaska);

        var captured = Assert.Single(attackAEvents.OfType<HeadquartersCaptured>());
        Assert.Equal(PlayerA, captured.Attacker);
        Assert.Equal(PlayerB, captured.OriginalOwner);
        Assert.Equal(Alaska, captured.Territory);

        var rowsAfterCapture = HeadquartersTray.Rows(engine.Observe(session.State!, PlayerA));
        var alaskaRow = Assert.Single(rowsAfterCapture, r => r.Declarer == PlayerB);
        Assert.Equal(PlayerA, alaskaRow.Holder);

        Assert.True(HeadquartersTray.HasLostOwnHeadquarters(engine.Observe(session.State!, PlayerB), PlayerB));

        EndPhase(session, PlayerA); // Attack -> Fortify
        EndPhase(session, PlayerA); // Fortify -> B's Reinforce

        // --- Player B's turn: recapture Alaska from Alberta ---
        Assert.Equal(PlayerB, session.State!.Turn.CurrentPlayer);
        Assert.Equal(TurnPhase.Reinforce, session.State!.Turn.Phase);
        PlaceAllReinforcementTroopsOn(session, Alberta);
        EndPhase(session, PlayerB);
        Assert.Equal(TurnPhase.Attack, session.State!.Turn.Phase);

        var attackBEvents = AttackAndOccupy(session, PlayerB, Alberta, Alaska);

        var recaptured = Assert.Single(attackBEvents.OfType<HeadquartersCaptured>());
        Assert.Equal(PlayerB, recaptured.Attacker);
        Assert.Equal(PlayerB, recaptured.OriginalOwner); // recapturing their own declared HQ
        Assert.Equal(Alaska, recaptured.Territory);

        Assert.False(HeadquartersTray.HasLostOwnHeadquarters(engine.Observe(session.State!, PlayerB), PlayerB));
        var rowsAfterRecapture = HeadquartersTray.Rows(engine.Observe(session.State!, PlayerA));
        var alaskaRowAfterRecapture = Assert.Single(rowsAfterRecapture, r => r.Declarer == PlayerB);
        Assert.Equal(PlayerB, alaskaRowAfterRecapture.Holder);
    }

    /// <summary>
    /// Regression safety (spec's "Regression safety for existing modes and
    /// phases"): proves the shared <c>CardPanel</c> Capital section stays
    /// inert on a non-Capital game — <see cref="HeadquartersTray.IsRevealed"/>
    /// stays false and <see cref="HeadquartersTray.Rows"/> stays empty in
    /// every phase, even though every viewer's <c>PlayerView</c> is observed
    /// directly (not just gated by <c>Session.State.Mode</c>).
    /// </summary>
    [Fact]
    public void Classic_game_never_reveals_or_lists_headquarters()
    {
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var session = new GameSessionService(engine, QueuedDiceRoller.ForRollOff(3));

        var rows = new List<PlayerSetupRow>
        {
            new("Ana", "#E53935", false),
            new("Beto", "#1E88E5", false),
            new("Caro", "#43A047", false)
        };
        var startResult = session.Start(rows, GameMode.Classic);
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(startResult);

        AssertHeadquartersTrayInert(session, engine);

        DriveClaimPhase(session, new Dictionary<PlayerId, Queue<TerritoryId>>());
        AssertHeadquartersTrayInert(session, engine);

        DriveSetupPhase(session, new Dictionary<PlayerId, TerritoryId>());
        Assert.Equal(TurnPhase.Reinforce, session.State!.Turn.Phase);
        AssertHeadquartersTrayInert(session, engine);
    }

    private static void AssertHeadquartersTrayInert(GameSessionService session, GameEngine engine)
    {
        foreach (var viewerId in new[] { PlayerA, PlayerB, PlayerC })
        {
            var view = engine.Observe(session.State!, viewerId);
            Assert.False(HeadquartersTray.IsRevealed(view));
            Assert.Empty(HeadquartersTray.Rows(view));
            Assert.False(HeadquartersTray.HasLostOwnHeadquarters(view, viewerId));
        }
    }

    /// <summary>
    /// Drives the round-robin Claim phase, honouring any per-player forced
    /// picks before falling back to "first unclaimed territory". The
    /// default fallback skips every territory still reserved in ANY
    /// player's forced queue — otherwise a later default pick (e.g. Player
    /// C's turn) could snipe a territory reserved for one of Player B's
    /// *later* forced turns before B gets to claim it.
    /// </summary>
    private static void DriveClaimPhase(GameSessionService session, IReadOnlyDictionary<PlayerId, Queue<TerritoryId>> forcedClaims)
    {
        while (session.State!.Turn.Phase == TurnPhase.Claim)
        {
            var state = session.State!;
            var actor = state.Turn.CurrentPlayer;

            TerritoryId territory;
            if (forcedClaims.TryGetValue(actor, out var queue) && queue.Count > 0 && state.Territories[queue.Peek()].Owner is null)
            {
                territory = queue.Dequeue();
            }
            else
            {
                var reserved = forcedClaims.Values.SelectMany(q => q).ToHashSet();
                territory = state.Territories.First(kv => kv.Value.Owner is null && !reserved.Contains(kv.Key)).Key;
            }

            var result = session.Execute(new ClaimTerritoryCommand(actor, territory, 1));
            Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        }
    }

    /// <summary>Drives the round-robin Setup phase, stacking every forced player's troops onto their designated territory and everyone else's onto whichever territory they own first.</summary>
    private static void DriveSetupPhase(GameSessionService session, IReadOnlyDictionary<PlayerId, TerritoryId> reinforceTarget)
    {
        while (session.State!.Turn.Phase == TurnPhase.Setup)
        {
            var state = session.State!;
            var actor = state.Turn.CurrentPlayer;
            var territory = reinforceTarget.TryGetValue(actor, out var target)
                ? target
                : state.Territories.First(kv => kv.Value.Owner == actor).Key;

            var result = session.Execute(new PlaceTroopsCommand(actor, territory, 1));
            if (result is CommandResult<GameState, GameEvent>.Rejected rejected)
            {
                Assert.Fail($"PlaceTroops {actor}->{territory} rejected: {rejected.Error.Code} - {rejected.Error.Message}.");
            }
            Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        }
    }

    /// <summary>
    /// Drives the round-robin SelectHeadquarters phase, honouring any forced
    /// picks and defaulting everyone else to their first owned territory.
    /// Asserts, after every pick except the last, that <see cref="HeadquartersTray.IsRevealed"/>
    /// stays false for every viewer — the critical anti-leak constraint,
    /// covering the intermediate state after SOME but not ALL players have
    /// picked, not merely the state before anyone has. Returns the actual
    /// per-player picks for later row/holder assertions.
    /// </summary>
    private static Dictionary<PlayerId, TerritoryId> DriveHeadquartersSelectionAssertingSecrecyThroughout(
        GameSessionService session, GameEngine engine, IReadOnlyDictionary<PlayerId, TerritoryId> forcedHeadquarters)
    {
        var declared = new Dictionary<PlayerId, TerritoryId>();
        var allPlayers = new[] { PlayerA, PlayerB, PlayerC };

        while (session.State!.Turn.Phase == TurnPhase.SelectHeadquarters)
        {
            var state = session.State!;
            var actor = state.Turn.CurrentPlayer;
            var territory = forcedHeadquarters.TryGetValue(actor, out var forced)
                ? forced
                : state.Territories.First(kv => kv.Value.Owner == actor).Key;
            declared[actor] = territory;

            var result = session.Execute(new SelectHeadquartersCommand(actor, territory));
            Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);

            if (session.State!.Turn.Phase == TurnPhase.SelectHeadquarters)
            {
                foreach (var viewerId in allPlayers)
                {
                    var view = engine.Observe(session.State!, viewerId);
                    Assert.False(HeadquartersTray.IsRevealed(view));
                    Assert.Empty(HeadquartersTray.Rows(view));
                }
            }
        }

        return declared;
    }

    private static void PlaceAllReinforcementTroopsOn(GameSessionService session, TerritoryId territory)
    {
        var actor = session.State!.Turn.CurrentPlayer;

        while (session.State!.Players.Single(p => p.Id == actor).TroopsRemaining > 0)
        {
            var result = session.Execute(new PlaceTroopsCommand(actor, territory, 1));
            Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        }
    }

    private static void EndPhase(GameSessionService session, PlayerId actor)
    {
        var result = session.Execute(new EndPhaseCommand(actor));
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
    }

    /// <summary>
    /// Attacks with 3 dice from an overwhelming staging territory,
    /// repeating rounds (the attacker never loses a troop against
    /// <see cref="AlwaysAttackerWinsDiceRoller"/>, but a multi-troop
    /// defender — e.g. a re-occupied, previously-captured HQ — may still
    /// need more than one round to reach 0) until the territory is
    /// conquered, then immediately resolves the resulting
    /// <see cref="PendingOccupation"/>.
    /// </summary>
    /// <summary>
    /// Returns the conquering round's own events (NOT <see cref="GameSessionService.LastEvents"/>,
    /// which the immediately-following <c>OccupyCommand</c> dispatch would
    /// overwrite) — callers assert <see cref="HeadquartersCaptured"/> against
    /// this return value, not the session's post-occupy state.
    /// </summary>
    private static IReadOnlyList<GameEvent> AttackAndOccupy(GameSessionService session, PlayerId actor, TerritoryId from, TerritoryId to)
    {
        const int maxRounds = 20;

        for (var round = 0; round < maxRounds; round++)
        {
            var attackResult = session.Execute(new AttackCommand(actor, from, to, 3));
            if (attackResult is CommandResult<GameState, GameEvent>.Rejected rejected)
            {
                Assert.Fail($"Attack {from}->{to} by {actor} rejected: {rejected.Error.Code} - {rejected.Error.Message}.");
            }
            var conqueringEvents = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(attackResult).Events;

            if (session.State!.Turn.PendingOccupation is not { } pending)
            {
                continue;
            }

            var occupyResult = session.Execute(new OccupyCommand(actor, pending.MinimumTroops));
            if (occupyResult is CommandResult<GameState, GameEvent>.Rejected rejectedOccupy)
            {
                Assert.Fail($"Occupy {pending.From}->{pending.Conquered} by {actor} rejected: {rejectedOccupy.Error.Code} - {rejectedOccupy.Error.Message}.");
            }
            Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(occupyResult);
            return conqueringEvents;
        }

        Assert.Fail($"Attack {from}->{to} by {actor} did not conquer within {maxRounds} rounds.");
        return [];
    }
}
