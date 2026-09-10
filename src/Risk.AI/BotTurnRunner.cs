using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;

namespace Risk.AI;

/// <summary>
/// The production Observe/Decide/Execute orchestrator (design's data flow
/// diagram) that finally connects <see cref="IBotPlayer"/> to a real
/// <see cref="IGameEngine"/>. Contains no <c>catch</c>, no retry, and no
/// fallback/substitute command (design D5): any <c>Rejected</c> result from
/// <see cref="IGameEngine.Execute"/> is a defect, surfaced as a terminal
/// <see cref="BotRunResult.Rejected"/> carrying the exact offending command
/// and <see cref="Risk.Domain.Errors.GameError"/> — the spec's "Zero-Rejected
/// invariant" and "BotTurnRunner illegality is a hard failure" requirements
/// are structural facts about this class, not conventions. An exception
/// thrown by a bot's own <see cref="IBotPlayer.DecideNextCommand"/> is
/// likewise never caught: it propagates straight to the caller, the same
/// "exceptions are for programmer errors" convention <c>GameEngine</c> itself
/// follows.
/// </summary>
/// <remarks>
/// <see cref="BotTurnRunner"/>, <see cref="BotRunResult"/>, and the shared
/// <see cref="BotTurnStep"/> stepping primitive are the only three
/// <c>Risk.AI</c> types allowed to name <see cref="GameState"/> (design D1;
/// <see cref="BotTurnStep"/> was extracted post-Phase-8 so this class'
/// stepping sequence and <c>Risk.AI.Tests</c>' <c>GameHarness</c> fixture
/// never drift apart, but it is pure orchestration plumbing with the exact
/// same D1 posture as this class) — and even across all three, only
/// <see cref="GameState.Status"/> and <see cref="GameState.Turn"/>.<see cref="TurnState.CurrentPlayer"/>
/// are ever read directly (both are also present on <see cref="Views.PlayerView"/>);
/// the rest of <see cref="GameState"/> is passed through opaquely to
/// <see cref="IGameEngine.Observe"/>/<see cref="IGameEngine.Execute"/>. No
/// <see cref="GameState"/> value, nor any fragment of one beyond those two
/// public-information reads, ever reaches an <see cref="IBotPlayer"/>.
/// </remarks>
public sealed class BotTurnRunner
{
    private readonly IGameEngine _engine;

    public BotTurnRunner(IGameEngine engine) => _engine = engine;

    /// <summary>
    /// Drives <paramref name="bot"/> through its own turn — every phase,
    /// including any pending occupation and mandatory trade along the way —
    /// stopping as soon as the turn passes to a different player, the game
    /// is won, or <paramref name="maxCommands"/> is reached.
    /// </summary>
    public BotRunResult RunTurn(
        GameState state,
        IBotPlayer bot,
        BotMemory memory,
        int maxCommands = Scoring.BotWeights.MaxCommandsPerTurn)
    {
        var turnOwner = state.Turn.CurrentPlayer;
        var bots = new Dictionary<PlayerId, IBotPlayer> { [bot.Id] = bot };
        var memories = new Dictionary<PlayerId, BotMemory> { [bot.Id] = memory };

        return Drive(state, bots, memories, maxCommands, stopWhen: s => s.Turn.CurrentPlayer != turnOwner);
    }

    /// <summary>
    /// Drives a full game — every seat played by the matching
    /// <paramref name="bots"/> entry — from <paramref name="state"/> until
    /// <see cref="GameStatus.Won"/>, a <c>Rejected</c> command, or
    /// <paramref name="maxCommands"/> is reached. Every registered bot's
    /// <see cref="BotMemory"/> is threaded independently; a fresh
    /// <see cref="BotMemory.Empty"/> is used for each on entry.
    /// </summary>
    public BotRunResult RunGame(
        GameState state,
        IReadOnlyList<IBotPlayer> bots,
        int maxCommands = Scoring.BotWeights.MaxCommandsPerGame)
    {
        var botsById = bots.ToDictionary(b => b.Id);
        var memories = bots.ToDictionary(b => b.Id, _ => BotMemory.Empty);

        return Drive(state, botsById, memories, maxCommands, stopWhen: static _ => false);
    }

    /// <summary>
    /// The shared stepping loop behind both <see cref="RunTurn"/> and
    /// <see cref="RunGame"/> (design's data flow diagram): checks the two
    /// terminal conditions (already won / turn boundary reached, budget
    /// exhausted), then delegates the actual observe/decide/execute/fold
    /// sequence to <see cref="BotTurnStep.Advance"/> — the same step
    /// <c>GameHarness</c> uses, so the two never drift apart.
    /// </summary>
    private BotRunResult Drive(
        GameState state,
        IReadOnlyDictionary<PlayerId, IBotPlayer> bots,
        Dictionary<PlayerId, BotMemory> memories,
        int maxCommands,
        Func<GameState, bool> stopWhen)
    {
        var commandsIssued = 0;

        while (true)
        {
            if (state.Status is GameStatus.Won || stopWhen(state))
            {
                return new BotRunResult.Completed(state, Snapshot(memories), commandsIssued);
            }

            if (commandsIssued >= maxCommands)
            {
                return new BotRunResult.Exhausted(state, Snapshot(memories), commandsIssued);
            }

            var currentPlayer = state.Turn.CurrentPlayer;

            if (!bots.TryGetValue(currentPlayer, out var actingBot))
            {
                throw new InvalidOperationException(
                    $"Unreachable: no registered bot for current player {currentPlayer.Value}.");
            }

            var (result, command, nextState) = BotTurnStep.Advance(_engine, state, currentPlayer, actingBot, memories);

            if (result is CommandResult<GameState, GameEvent>.Rejected rejected)
            {
                return new BotRunResult.Rejected(state, Snapshot(memories), commandsIssued, command, rejected.Error);
            }

            state = nextState;
            commandsIssued++;
        }
    }

    private static IReadOnlyDictionary<PlayerId, BotMemory> Snapshot(Dictionary<PlayerId, BotMemory> memories) =>
        new Dictionary<PlayerId, BotMemory>(memories);
}
