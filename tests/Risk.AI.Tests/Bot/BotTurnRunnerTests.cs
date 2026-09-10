using Risk.AI.Tests.Fakes;
using Risk.Domain.Errors;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.State;
using Risk.Engine.Views;

namespace Risk.AI.Tests.Bot;

/// <summary>
/// Tests <see cref="BotTurnRunner"/> — the Observe/Decide/Execute
/// orchestrator (design's data flow diagram) — against a REAL
/// <see cref="GameEngine"/> via <see cref="GameHarness"/>, never a hand-built
/// <c>GameState</c>. The single most important test in this file is the
/// hard-fail negative test: the spec's "BotTurnRunner illegality is a hard
/// failure" requirement is non-negotiable, so
/// <see cref="RunTurn_surfaces_a_rejected_command_as_a_terminal_result_without_any_fallback"/>
/// proves it by feeding the runner a deliberately-illegal stub bot rather
/// than waiting for a real decision bug to exist.
/// </summary>
public class BotTurnRunnerTests
{
    private static readonly PlayerId Player0 = new(0);
    private static readonly PlayerId Player1 = new(1);

    /// <summary>Always returns a command whose Actor can never be the current player — guaranteed NotYourTurn.</summary>
    private sealed class AlwaysIllegalBot : IBotPlayer
    {
        public AlwaysIllegalBot(PlayerId id) => Id = id;

        public PlayerId Id { get; }

        public (GameCommand Command, BotMemory Memory) DecideNextCommand(PlayerView view, BotMemory memory) =>
            (new EndPhaseCommand(new PlayerId(9999)), memory);
    }

    /// <summary>Never returns — always throws, to prove the runner never swallows a bot's own exception.</summary>
    private sealed class ThrowingBot : IBotPlayer
    {
        public ThrowingBot(PlayerId id) => Id = id;

        public PlayerId Id { get; }

        public (GameCommand Command, BotMemory Memory) DecideNextCommand(PlayerView view, BotMemory memory) =>
            throw new InvalidOperationException("ThrowingBot: deliberate test failure, never caught by BotTurnRunner.");
    }

    // --- 8.2 RunTurn drives a full turn against a real engine ---

    [Fact]
    public void RunTurn_drives_every_phase_of_a_turn_and_advances_to_the_next_player()
    {
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller())
            .FastForwardToFirstReinforce();
        var actor = harness.State.Turn.CurrentPlayer;
        var bot = new FirstLegalBot(actor);
        var runner = new BotTurnRunner(harness.Engine);

        var result = runner.RunTurn(harness.State, bot, BotMemory.Empty);

        var completed = Assert.IsType<BotRunResult.Completed>(result);
        Assert.NotEqual(actor, completed.State.Turn.CurrentPlayer);
        Assert.True(completed.CommandsIssued > 0);
        Assert.True(completed.Memories.ContainsKey(actor));
    }

    // --- 8.3 hard-fail negative test (non-negotiable, per spec) ---

    [Fact]
    public void RunTurn_surfaces_a_rejected_command_as_a_terminal_result_without_any_fallback()
    {
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller())
            .FastForwardToFirstReinforce();
        var stateBefore = harness.State;
        var actor = stateBefore.Turn.CurrentPlayer;
        var illegalBot = new AlwaysIllegalBot(actor);
        var runner = new BotTurnRunner(harness.Engine);

        var result = runner.RunTurn(stateBefore, illegalBot, BotMemory.Empty);

        var rejected = Assert.IsType<BotRunResult.Rejected>(result);
        Assert.Equal(stateBefore, rejected.State);
        Assert.Equal(0, rejected.CommandsIssued);
        Assert.Equal(new EndPhaseCommand(new PlayerId(9999)), rejected.Command);
        Assert.Equal(GameErrorCode.NotYourTurn, rejected.Error.Code);
    }

    [Fact]
    public void RunGame_surfaces_a_rejected_command_the_same_way_as_RunTurn()
    {
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller())
            .FastForwardToFirstReinforce();
        var actor = harness.State.Turn.CurrentPlayer;
        var others = harness.State.Players.Where(p => !p.IsNeutral && p.Id != actor).Select(p => p.Id);
        IReadOnlyList<IBotPlayer> bots = new IBotPlayer[] { new AlwaysIllegalBot(actor) }
            .Concat(others.Select(id => (IBotPlayer)new FirstLegalBot(id)))
            .ToArray();
        var runner = new BotTurnRunner(harness.Engine);

        var result = runner.RunGame(harness.State, bots);

        var rejected = Assert.IsType<BotRunResult.Rejected>(result);
        Assert.Equal(0, rejected.CommandsIssued);
        Assert.Equal(GameErrorCode.NotYourTurn, rejected.Error.Code);
    }

    [Fact]
    public void RunTurn_lets_an_exception_thrown_by_the_bot_itself_propagate_uncaught()
    {
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller())
            .FastForwardToFirstReinforce();
        var actor = harness.State.Turn.CurrentPlayer;
        var throwingBot = new ThrowingBot(actor);
        var runner = new BotTurnRunner(harness.Engine);

        Assert.Throws<InvalidOperationException>(() => { runner.RunTurn(harness.State, throwingBot, BotMemory.Empty); });
    }

    [Fact]
    public void RunGame_lets_an_exception_thrown_by_a_bot_propagate_uncaught()
    {
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller())
            .FastForwardToFirstReinforce();
        IReadOnlyList<IBotPlayer> bots = harness.State.Players
            .Where(p => !p.IsNeutral)
            .Select(p => (IBotPlayer)new ThrowingBot(p.Id))
            .ToArray();
        var runner = new BotTurnRunner(harness.Engine);

        Assert.Throws<InvalidOperationException>(() => { runner.RunGame(harness.State, bots); });
    }

    // --- Game-over boundary, RunTurn variant (mirrors the RunGame case below) ---

    [Fact]
    public void RunTurn_stops_cleanly_on_an_already_won_state_without_any_further_Observe_or_Execute()
    {
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller())
            .FastForwardToFirstReinforce();
        var wonState = harness.State with { Status = new GameStatus.Won(Player0) };
        var actor = harness.State.Turn.CurrentPlayer;
        var throwingBot = new ThrowingBot(actor);
        var runner = new BotTurnRunner(harness.Engine);

        var result = runner.RunTurn(wonState, throwingBot, BotMemory.Empty);

        var completed = Assert.IsType<BotRunResult.Completed>(result);
        Assert.Equal(wonState, completed.State);
        Assert.Equal(0, completed.CommandsIssued);
    }

    // --- 8.4 Exhausted when the command budget is reached ---

    [Fact]
    public void RunGame_returns_Exhausted_when_the_budget_is_reached_before_the_game_ends()
    {
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller())
            .FastForwardToFirstReinforce();
        IReadOnlyList<IBotPlayer> bots = harness.State.Players
            .Where(p => !p.IsNeutral)
            .Select(p => (IBotPlayer)new FirstLegalBot(p.Id))
            .ToArray();
        var runner = new BotTurnRunner(harness.Engine);

        var result = runner.RunGame(harness.State, bots, maxCommands: 5);

        var exhausted = Assert.IsType<BotRunResult.Exhausted>(result);
        Assert.Equal(5, exhausted.CommandsIssued);
    }

    [Fact]
    public void RunGame_returns_Exhausted_immediately_when_maxCommands_is_zero_without_deciding_anything()
    {
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller())
            .FastForwardToFirstReinforce();
        // A ThrowingBot proves the budget check happens BEFORE any
        // Observe/DecideNextCommand call: if the runner ever asked this bot
        // to decide, the test would fail with an exception instead of
        // asserting Exhausted.
        IReadOnlyList<IBotPlayer> bots = harness.State.Players
            .Where(p => !p.IsNeutral)
            .Select(p => (IBotPlayer)new ThrowingBot(p.Id))
            .ToArray();
        var runner = new BotTurnRunner(harness.Engine);

        var result = runner.RunGame(harness.State, bots, maxCommands: 0);

        var exhausted = Assert.IsType<BotRunResult.Exhausted>(result);
        Assert.Equal(0, exhausted.CommandsIssued);
        Assert.Equal(harness.State, exhausted.State);
    }

    // --- Game-over boundary: no extra Observe/Execute past GameStatus.Won ---

    [Fact]
    public void RunGame_stops_cleanly_on_an_already_won_state_without_any_further_Observe_or_Execute()
    {
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller())
            .FastForwardToFirstReinforce();
        var wonState = harness.State with { Status = new GameStatus.Won(Player0) };
        // A ThrowingBot proves no further Observe/DecideNextCommand cycle is
        // attempted once the game is already won.
        IReadOnlyList<IBotPlayer> bots = harness.State.Players
            .Where(p => !p.IsNeutral)
            .Select(p => (IBotPlayer)new ThrowingBot(p.Id))
            .ToArray();
        var runner = new BotTurnRunner(harness.Engine);

        var result = runner.RunGame(wonState, bots);

        var completed = Assert.IsType<BotRunResult.Completed>(result);
        Assert.Equal(wonState, completed.State);
        Assert.Equal(0, completed.CommandsIssued);
    }

    // --- Binding contract: WithSeenActor wired for every actor, every iteration (design D8) ---

    [Fact]
    public void RunGame_wires_WithSeenActor_for_every_actor_so_the_TwoPlayer_neutral_is_deduced_by_elimination()
    {
        // TwoPlayerSetupStrategy appends the neutral as players.Count, i.e.
        // PlayerId(2) for a 2-player game — verified directly against
        // Modes/TwoPlayerSetupStrategy.cs.
        var neutral = new PlayerId(2);
        var harness = GameHarness.Start(GameMode.TwoPlayer, 2, new SequenceDiceRoller());
        IReadOnlyList<IBotPlayer> bots = [new BotPlayer(Player0), new BotPlayer(Player1)];
        var runner = new BotTurnRunner(harness.Engine);

        // TwoPlayer's Setup per-turn budget is 2 troops, placed 1 at a time
        // (design's Setup algorithm always places exactly 1), so 4 commands
        // covers exactly one full turn each for both real players — enough
        // for each to have observed the OTHER as TurnState.CurrentPlayer at
        // least once, without needing to drive all the way to Reinforce.
        var result = runner.RunGame(harness.State, bots, maxCommands: 4);

        var exhausted = Assert.IsType<BotRunResult.Exhausted>(result);
        Assert.Equal(4, exhausted.CommandsIssued);

        foreach (var id in new[] { Player0, Player1 })
        {
            var memory = exhausted.Memories[id];
            Assert.Contains(Player0, memory.SeenActors);
            Assert.Contains(Player1, memory.SeenActors);

            var view = harness.Engine.Observe(exhausted.State, id);
            Assert.Equal(neutral, memory.FindTwoPlayerNeutral(view, id));
        }
    }
}
