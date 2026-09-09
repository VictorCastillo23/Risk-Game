using Risk.Domain.Errors;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.State;

namespace Risk.AI;

/// <summary>
/// The outcome of driving one or more turns through <see cref="BotTurnRunner"/>
/// — mirrors <see cref="Risk.Engine.Results.CommandResult{TState,TEvent}"/>'s
/// exact closed shape (design D5). <see cref="Rejected"/> is a TERMINAL exit
/// carrying the offending command and the engine's own
/// <see cref="GameError"/>: the spec's hard-fail requirement means a
/// <c>Rejected</c> is a defect, never something <see cref="BotTurnRunner"/>
/// catches, retries, or substitutes a fallback command for.
/// <see cref="Exhausted"/> is a distinct case (not folded into
/// <see cref="Completed"/>) so a test or caller can tell "the game just
/// hasn't finished yet" apart from "the game actually won" without
/// inspecting <see cref="Risk.Engine.State.GameState.Status"/> itself.
/// </summary>
public abstract record BotRunResult
{
    private BotRunResult()
    {
    }

    /// <summary>
    /// The driven turn(s) ended cleanly: either <see cref="GameStatus.Won"/>
    /// was reached (a full <see cref="BotTurnRunner.RunGame"/>), or the turn
    /// simply passed to the next player (a single <see cref="BotTurnRunner.RunTurn"/>)
    /// with every command along the way accepted by the engine.
    /// </summary>
    public sealed record Completed(
        GameState State,
        IReadOnlyDictionary<PlayerId, BotMemory> Memories,
        int CommandsIssued) : BotRunResult;

    /// <summary>
    /// The engine rejected a command a bot issued — the hard-fail terminal
    /// case (spec's "Zero-Rejected invariant", design D5). <see cref="State"/>
    /// is the state immediately BEFORE the rejected command (the engine
    /// leaves state unchanged on <c>Rejected</c>), never a substitute or
    /// rescued state.
    /// </summary>
    public sealed record Rejected(
        GameState State,
        IReadOnlyDictionary<PlayerId, BotMemory> Memories,
        int CommandsIssued,
        GameCommand Command,
        GameError Error) : BotRunResult;

    /// <summary>
    /// The command budget (<c>maxCommands</c>) was reached before the driven
    /// turn(s) ended, with every command along the way still accepted by the
    /// engine — a budget-tuning signal, not a rule violation.
    /// </summary>
    public sealed record Exhausted(
        GameState State,
        IReadOnlyDictionary<PlayerId, BotMemory> Memories,
        int CommandsIssued) : BotRunResult;
}
