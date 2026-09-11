using Risk.AI;
using Risk.Domain.Errors;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Web.Models;
using Risk.Web.Persistence;
using Risk.Web.Services;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Services;

/// <summary>
/// Phase 2/3 of <c>sdd/risk-web-ai-seats</c>: proves
/// <see cref="GameSessionService.AdvanceAiTurns"/> (private — exercised only
/// through <see cref="GameSessionService.Start"/>/<see cref="GameSessionService.Execute"/>/
/// <see cref="GameSessionService.LoadFrom"/>, exactly like every other
/// caller) resolves AI-controlled seats before control returns to the
/// caller, never masks a bot's illegal command, and keeps
/// <see cref="GameSessionService.LastEvents"/> scoped to the caller's own
/// command (spec's "LastEvents scope excludes AI-turn events", design D6).
///
/// Every scenario below drives real engine rules through a real
/// <see cref="GameEngine"/> (or, for the hard-fail scenario, a thin
/// rejecting decorator over one — <see cref="RejectAfterGameEngine"/>) and a
/// real <see cref="BotPlayer"/> via <see cref="BotTurnRunner"/> — no
/// <see cref="FakeGameEngine"/> stubbing here, because the whole point is to
/// prove the real Observe/Decide/Execute stack composes correctly through
/// this new seam.
/// </summary>
public class GameSessionServiceAiIntegrationTests
{
    private static readonly PlayerId Seat0 = new(0);
    private static readonly PlayerId Seat1 = new(1);
    private static readonly PlayerId Seat2 = new(2);

    /// <summary>
    /// Scenario (a): an all-AI game resolves entirely from a single
    /// <see cref="GameSessionService.Start"/> call — <c>Start</c> itself
    /// rebuilds the bot registry and drains every AI turn (design's
    /// ordering-of-operations pseudocode) before it ever returns, so a
    /// caller that never issues a single <see cref="GameSessionService.Execute"/>
    /// call still ends up at <see cref="GameStatus.Won"/>.
    /// </summary>
    [Fact]
    public void An_all_AI_game_resolves_to_Won_from_a_single_Start_call_with_zero_Execute_calls()
    {
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var session = new GameSessionService(
            engine, new AlwaysAttackerWinsDiceRoller(), new FakeGameStore(),
            StubAuthenticationStateProvider.Anonymous(), new BotTurnRunner(engine));

        var rows = new List<PlayerSetupRow>
        {
            new("Bot A", "#E53935", true),
            new("Bot B", "#1E88E5", true),
        };

        var startResult = session.Start(rows, GameMode.TwoPlayer);

        // Start's OWN result must still be the caller's (GameSetup.Create's
        // initial Ok), never swapped out for anything AdvanceAiTurns did —
        // "return result — the CALLER's result, never the AI's" (design's
        // ordering-of-operations note).
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(startResult);

        Assert.NotNull(session.State);
        Assert.IsType<GameStatus.Won>(session.State!.Status);
        Assert.Null(session.AiFailure);
    }

    /// <summary>
    /// Scenario (b): a human's <see cref="GameSessionService.Execute"/> call
    /// hands off to exactly one AI seat, and that seat's own turn is fully
    /// resolved before <c>Execute</c> returns — control never comes back to
    /// the caller sitting on an AI-controlled <c>Turn.CurrentPlayer</c>.
    /// Also doubles as the D6 "LastEvents scope excludes AI-turn events"
    /// proof: both seats emit the exact same event TYPE
    /// (<see cref="TroopsPlaced"/>) in this one call, so asserting
    /// <c>LastEvents</c> contains only the human's own is a precise,
    /// unambiguous check — not one that could pass by accident because the
    /// two events merely look different.
    /// </summary>
    [Fact]
    public void A_human_Execute_call_hands_off_to_one_AI_seat_resolved_before_it_returns()
    {
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var session = new GameSessionService(
            engine, new AlwaysAttackerWinsDiceRoller(), new FakeGameStore(),
            StubAuthenticationStateProvider.Anonymous(), new BotTurnRunner(engine));

        var rows = new List<PlayerSetupRow>
        {
            new("Human", "#E53935", false),
            new("Bot", "#1E88E5", true),
        };
        var startResult = session.Start(rows, GameMode.TwoPlayer);
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(startResult);

        // TwoPlayer never rolls TurnOrder's dice — players[0] always opens Setup.
        Assert.Equal(Seat0, session.State!.Turn.CurrentPlayer);

        // TwoPlayer's Setup placement budget is 2 troops per turn
        // (GameEngine.SetupTroopsPerTurn); the turn only advances once
        // TroopsRemaining is back to an even multiple of 2, so the human
        // must spend the FULL per-turn budget in one command to hand off to
        // the AI seat within this single Execute call.
        var humanTerritory = session.State!.Territories.First(kv => kv.Value.Owner == Seat0).Key;
        var result = session.Execute(new PlaceTroopsCommand(Seat0, humanTerritory, 2));
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);

        Assert.Equal(Seat0, session.State!.Turn.CurrentPlayer);
        Assert.False(session.Players[session.State!.Turn.CurrentPlayer].IsAi);
        Assert.Null(session.AiFailure);

        // D6: LastEvents is the human's OWN command's delta only.
        var placed = Assert.Single(session.LastEvents);
        var troopsPlaced = Assert.IsType<TroopsPlaced>(placed);
        Assert.Equal(Seat0, troopsPlaced.Player);

        // The bot's own placement genuinely happened (not skipped) — it's
        // just excluded from LastEvents, per D6. The full Log has both.
        var loggedPlacements = session.State!.Log.OfType<TroopsPlaced>().ToList();
        Assert.Contains(loggedPlacements, e => e.Player == Seat0);
        Assert.Contains(loggedPlacements, e => e.Player == Seat1);
    }

    /// <summary>
    /// Scenario (c): a human's Claim-phase command chains through TWO
    /// consecutive AI seats in one <see cref="GameSessionService.Execute"/>
    /// call. <see cref="GameEngine.AdvanceAfterClaim"/>-equivalent rotation
    /// is a plain round-robin (no eligibility skip), so after the human
    /// claims, Bot1 then Bot2 must each claim exactly one territory before
    /// control returns to the human — proving <c>AdvanceAiTurns</c>' outer
    /// <c>while</c> genuinely loops across multiple seats, not just one.
    /// </summary>
    [Fact]
    public void A_human_Execute_call_chains_through_two_consecutive_AI_seats()
    {
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var session = new GameSessionService(
            engine, QueuedDiceRoller.ForRollOff(3), new FakeGameStore(),
            StubAuthenticationStateProvider.Anonymous(), new BotTurnRunner(engine));

        var rows = new List<PlayerSetupRow>
        {
            new("Human", "#E53935", false),
            new("Bot 1", "#1E88E5", true),
            new("Bot 2", "#43A047", true),
        };
        var startResult = session.Start(rows, GameMode.Classic);
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(startResult);

        // QueuedDiceRoller.ForRollOff(3) guarantees Seat0 wins TurnOrder's roll-off.
        Assert.Equal(Seat0, session.State!.Turn.CurrentPlayer);
        Assert.Equal(TurnPhase.Claim, session.State!.Turn.Phase);

        var unclaimed = session.State!.Territories.First(kv => kv.Value.Owner is null).Key;
        var result = session.Execute(new ClaimTerritoryCommand(Seat0, unclaimed, 1));
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);

        // Control is back at the human, both AI seats already resolved.
        Assert.Equal(Seat0, session.State!.Turn.CurrentPlayer);
        Assert.False(session.Players[session.State!.Turn.CurrentPlayer].IsAi);
        Assert.Null(session.AiFailure);

        // Human's 1 claim + Bot1's 1 + Bot2's 1 = 3 territories now owned.
        Assert.Equal(3, session.State!.Territories.Count(kv => kv.Value.Owner is not null));
        Assert.Contains(session.State!.Territories.Values, t => t.Owner == Seat1);
        Assert.Contains(session.State!.Territories.Values, t => t.Owner == Seat2);
    }

    /// <summary>
    /// Scenario (d): <see cref="GameSessionService.LoadFrom"/> onto a
    /// hand-built <see cref="GameSnapshot"/> whose <c>Turn.CurrentPlayer</c>
    /// is configured AI resolves it rather than sitting stuck on that seat —
    /// design D4's <c>BotMemory.Empty</c> path (a resumed AI seat always
    /// starts a fresh turn, so <c>Empty</c> is correct by construction).
    /// </summary>
    [Fact]
    public void LoadFrom_onto_an_AI_controlled_seat_resolves_it_rather_than_stalling()
    {
        var setupEngine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var setupSession = new GameSessionService(
            setupEngine, new AlwaysAttackerWinsDiceRoller(), new FakeGameStore(),
            StubAuthenticationStateProvider.Anonymous(), new BotTurnRunner(setupEngine));

        var rows = new List<PlayerSetupRow>
        {
            new("Ana", "#E53935", false),
            new("Beto", "#1E88E5", false),
        };
        var startResult = setupSession.Start(rows, GameMode.TwoPlayer);
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(startResult);

        var snapshot = setupSession.Snapshot();
        var aiSeat = snapshot.State.Turn.CurrentPlayer; // Seat0, human in this snapshot's own config
        Assert.Equal(Seat0, aiSeat);

        // Re-configure that seat as AI-controlled — simulating a resumed
        // session where this seat is now piloted by a bot.
        var reconfiguredPlayers = snapshot.Players
            .Select(p => p.Id == aiSeat ? p with { IsAi = true } : p)
            .ToList();
        var aiControlledSnapshot = snapshot with { Players = reconfiguredPlayers };

        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var session = new GameSessionService(
            engine, new AlwaysAttackerWinsDiceRoller(), new FakeGameStore(),
            StubAuthenticationStateProvider.Anonymous(), new BotTurnRunner(engine));

        session.LoadFrom(aiControlledSnapshot);

        Assert.True(session.ConfigFor(aiSeat).IsAi);
        Assert.NotEqual(aiSeat, session.State!.Turn.CurrentPlayer);
        Assert.Equal(Seat1, session.State!.Turn.CurrentPlayer);
        Assert.False(session.Players[session.State!.Turn.CurrentPlayer].IsAi);
        Assert.Null(session.AiFailure);
    }

    /// <summary>
    /// Scenario (e): the engine rejecting the AI's very first command is a
    /// hard failure — <see cref="GameSessionService.AiFailure"/> is set with
    /// the exact offending player/command/error, <c>State</c> stays at the
    /// pre-command value (the human's own successful command, unchanged by
    /// the AI attempt that never took effect), and the game does NOT
    /// retry/auto-recover: <see cref="RejectAfterGameEngine.Rejections"/>
    /// must be exactly 1, never higher.
    /// </summary>
    [Fact]
    public void A_Rejected_bot_command_sets_AiFailure_leaves_State_unchanged_and_never_retries()
    {
        var realEngine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var fakeEngine = new RejectAfterGameEngine(realEngine, rejectFor: Seat1);
        var session = new GameSessionService(
            fakeEngine, new AlwaysAttackerWinsDiceRoller(), new FakeGameStore(),
            StubAuthenticationStateProvider.Anonymous(), new BotTurnRunner(fakeEngine));

        var rows = new List<PlayerSetupRow>
        {
            new("Human", "#E53935", false),
            new("Bot", "#1E88E5", true),
        };
        var startResult = session.Start(rows, GameMode.TwoPlayer);
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(startResult);
        Assert.Equal(Seat0, session.State!.Turn.CurrentPlayer);

        // Same TwoPlayer 2-troops-per-turn budget as the handoff scenario
        // above — the human must spend the full per-turn budget in one
        // command to hand off to the AI seat within this Execute call.
        var humanTerritory = session.State!.Territories.First(kv => kv.Value.Owner == Seat0).Key;
        var result = session.Execute(new PlaceTroopsCommand(Seat0, humanTerritory, 2));

        // The CALLER's own result is unaffected by the AI's rejection — the
        // human's command genuinely succeeded.
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Same(ok.State, session.State);

        // The engine advanced Turn.CurrentPlayer to the bot before the bot's
        // own (rejected) command was ever attempted — that part of state
        // change is the human's own doing, not the AI's.
        Assert.Equal(Seat1, session.State!.Turn.CurrentPlayer);

        var failure = session.AiFailure;
        Assert.NotNull(failure);
        Assert.Equal(Seat1, failure!.Player);
        Assert.NotNull(failure.Command);
        Assert.False(failure.IsBudgetExhausted);
        Assert.NotNull(failure.Error);
        Assert.Equal(GameErrorCode.NotYourTurn, failure.Error!.Code);

        // No retry: the fake was asked to reject exactly once.
        Assert.Equal(1, fakeEngine.Rejections);
    }

    /// <summary>
    /// Fresh-context review fix (Gap 1): the handoff scenario above only
    /// ever plays ONE round-trip, which never reaches
    /// <c>GameMode.TwoPlayer</c>'s Setup Phase B — so it can never prove the
    /// <c>Execute</c>-side <c>MarkSeenByAllBots(command.Actor)</c> call site
    /// (the one that records a HUMAN's own turn as "seen" by every
    /// registered bot) is load-bearing. Without it, a bot's
    /// <c>BotMemory.SeenActors</c> would get stuck at <c>{ self }</c> forever
    /// (see <see cref="AdvanceAiTurns"/>'s own "Post-RED fix" doc comment),
    /// <c>Decisions.SetupDecision.IsTwoPlayerPhaseB</c> would never trip, and
    /// the bot would keep re-submitting an already-exhausted
    /// <c>PlaceTroopsCommand</c> — a genuine <c>Rejected</c> the moment its
    /// own Setup pool hits 0. This test drives a REAL human (scripted,
    /// purely defensive — places troops, never attacks, always
    /// <see cref="EndPhaseCommand"/>s Attack/Fortify) against a REAL AI seat
    /// all the way to <see cref="GameStatus.Won"/>, so both Setup Phase A
    /// AND Phase B are genuinely exercised for BOTH seats, and the AI's own
    /// aggression (<see cref="AlwaysAttackerWinsDiceRoller"/> guarantees
    /// every attack it launches succeeds) is what ends the game.
    /// </summary>
    [Fact]
    public void A_human_vs_AI_TwoPlayer_game_reaches_Setup_Phase_B_and_Won_with_no_AiFailure()
    {
        const int MaxCommands = 5_000; // safety net: fail loudly instead of hanging if something stalls

        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var session = new GameSessionService(
            engine, new AlwaysAttackerWinsDiceRoller(), new FakeGameStore(),
            StubAuthenticationStateProvider.Anonymous(), new BotTurnRunner(engine));

        var rows = new List<PlayerSetupRow>
        {
            new("Human", "#E53935", false),
            new("Bot", "#1E88E5", true),
        };
        var startResult = session.Start(rows, GameMode.TwoPlayer);
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(startResult);

        var commandsIssued = 0;

        while (session.State!.Turn.Phase == TurnPhase.Setup)
        {
            Assert.True(++commandsIssued < MaxCommands, "Setup phase exceeded the safety command cap.");

            // The bot's own Setup turns (Phase A and B alike) are fully
            // resolved inside Execute, below, before control ever returns
            // here — so whenever WE observe the game from outside, it must
            // always be the human's turn.
            Assert.Equal(Seat0, session.State!.Turn.CurrentPlayer);
            Assert.Null(session.AiFailure);

            DriveOneHumanSetupPlacement(session, Seat0);
        }

        Assert.Null(session.AiFailure);

        while (session.State!.Status is not GameStatus.Won)
        {
            Assert.True(++commandsIssued < MaxCommands, "Main game loop exceeded the safety command cap.");

            var state = session.State!;
            Assert.Equal(Seat0, state.Turn.CurrentPlayer);
            Assert.Null(session.AiFailure);

            // The human is purely defensive: places reinforcements, never
            // attacks, never fortifies. Only the bot ever attacks — with
            // AlwaysAttackerWinsDiceRoller guaranteeing every one of its
            // attacks succeeds, it eventually eliminates the human.
            var result = state.Turn.Phase switch
            {
                TurnPhase.Reinforce when state.Players.Single(p => p.Id == Seat0).TroopsRemaining > 0 =>
                    session.Execute(new PlaceTroopsCommand(Seat0, state.Territories.First(kv => kv.Value.Owner == Seat0).Key, 1)),
                _ => session.Execute(new EndPhaseCommand(Seat0)),
            };

            Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        }

        Assert.Null(session.AiFailure);
        var won = Assert.IsType<GameStatus.Won>(session.State!.Status);

        // The bot (Seat1) wins by eliminating the purely-defensive human —
        // proving the AI's own Attack-phase decisions ran correctly for a
        // full game, not just a handful of Setup commands.
        Assert.Equal(Seat1, won.Winner);

        // Setup Phase B genuinely ran for BOTH seats (design D8's
        // Phase A/B boundary) — the exact fact Gap 1's fix makes possible:
        // without it, the bot's own IsTwoPlayerPhaseB check would never
        // trip, and this event would never appear.
        var neutralPlacements = session.State!.Log.OfType<NeutralTroopsPlaced>().ToList();
        Assert.NotEmpty(neutralPlacements);
        Assert.Contains(neutralPlacements, e => e.Placer == Seat0);
        Assert.Contains(neutralPlacements, e => e.Placer == Seat1);
    }

    /// <summary>Mirrors <c>Risk.Tests.Fakes.GameSimulation.PlaceOneStartingTroop</c>, scoped to the human seat only (the bot's own Setup turns auto-resolve inside <see cref="GameSessionService.Execute"/>).</summary>
    private static void DriveOneHumanSetupPlacement(GameSessionService session, PlayerId human)
    {
        var state = session.State!;
        var humanPool = state.Players.Single(p => p.Id == human).TroopsRemaining;

        if (humanPool == 0)
        {
            // Setup Phase B: the human's own pool is drained, so this
            // command places one of the NEUTRAL's remaining troops.
            var neutralId = state.Players.Single(p => p.IsNeutral).Id;
            var neutralTerritory = state.Territories.First(kv => kv.Value.Owner == neutralId).Key;
            var neutralResult = session.Execute(new PlaceNeutralTroopsCommand(human, neutralTerritory, 1));
            Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(neutralResult);
            return;
        }

        var territory = state.Territories.First(kv => kv.Value.Owner == human).Key;
        var result = session.Execute(new PlaceTroopsCommand(human, territory, 1));
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
    }

    /// <summary>
    /// Fresh-context review fix (Gap 2): <see cref="AdvanceAiTurns"/>'s outer
    /// per-drain budget check and the <c>BotRunResult.Exhausted</c> switch
    /// arm were new production code with zero test coverage — the REAL
    /// budget (<c>BotWeights.MaxCommandsPerGame</c>, 250,000) makes hitting
    /// it directly impractical. The internal
    /// <see cref="GameSessionService.Start(IReadOnlyList{PlayerSetupRow}, GameMode, int)"/>
    /// test seam lets this test force the budget down to 1, deterministically
    /// tripping <see cref="AiTurnFailure.BudgetExhausted"/> after the FIRST
    /// bot (Seat0, TwoPlayer's <c>players[0]</c>) completes its own
    /// 2-command Setup turn (TwoPlayer's Setup budget is 2 troops per turn,
    /// and <c>Decisions.SetupDecision</c> always places exactly 1 troop per
    /// command — see <see cref="GameEngine.SetupTroopsPerTurn"/> — so
    /// Seat0's first turn always costs exactly 2 commands), before Seat1 (the
    /// OTHER bot) ever gets a turn.
    /// </summary>
    [Fact]
    public void An_exhausted_per_drain_budget_sets_AiFailure_and_preserves_partial_progress()
    {
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var session = new GameSessionService(
            engine, new AlwaysAttackerWinsDiceRoller(), new FakeGameStore(),
            StubAuthenticationStateProvider.Anonymous(), new BotTurnRunner(engine));

        var rows = new List<PlayerSetupRow>
        {
            new("Bot A", "#E53935", true),
            new("Bot B", "#1E88E5", true),
        };

        var startResult = session.Start(rows, GameMode.TwoPlayer, aiTurnBudget: 1);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(startResult);
        Assert.NotNull(session.State);
        Assert.IsType<GameStatus.InProgress>(session.State!.Status);

        // Seat0's own 2-command Setup turn completed (the budget check only
        // runs BEFORE a seat's turn starts, never mid-turn), then the
        // outer check tripped before Seat1 ever got to act.
        Assert.Equal(Seat1, session.State!.Turn.CurrentPlayer);

        var failure = session.AiFailure;
        Assert.NotNull(failure);
        Assert.Equal(Seat1, failure!.Player);
        Assert.True(failure.IsBudgetExhausted);
        Assert.Null(failure.Command);
        Assert.Null(failure.Error);

        // State reflects the genuine partial progress made before the
        // budget cut the drain off — not corrupted, not reverted: exactly
        // Seat0's own 2 TroopsPlaced commands, nothing from Seat1.
        var placements = session.State!.Log.OfType<TroopsPlaced>().ToList();
        Assert.Equal(2, placements.Count);
        Assert.All(placements, e => Assert.Equal(Seat0, e.Player));
    }
}
