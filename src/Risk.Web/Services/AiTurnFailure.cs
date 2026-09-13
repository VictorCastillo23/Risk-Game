using Risk.Domain.Errors;
using Risk.Domain.Players;
using Risk.Engine.Commands;

namespace Risk.Web.Services;

/// <summary>
/// Surfaces an AI seat's turn that could not be resolved to completion by
/// <see cref="GameSessionService.AdvanceAiTurns"/> — either the engine
/// rejected a command the bot issued (<see cref="Rejected"/>, design D1/D2:
/// a bot defect, never retried or masked), or the per-drain command budget
/// (<see cref="Risk.AI.Scoring.BotWeights.MaxCommandsPerGame"/>) ran out
/// first (<see cref="BudgetExhausted"/>, design D3: a tuning signal, not a
/// rule violation). Deliberately its own <c>Risk.Web</c>-only record, not a
/// third <see cref="Risk.Engine.Results.CommandResult{TState,TEvent}"/> case
/// or a reuse of <see cref="GameErrorCode"/>: an AI failure is for an actor
/// and command the caller never issued, so folding it into the caller's own
/// dispatch result would break "one dispatch, one result" (design D1).
/// </summary>
/// <param name="Player">The AI seat whose turn could not be resolved.</param>
/// <param name="Command">
/// The specific command the engine rejected, or <see langword="null"/> when
/// this failure is a <see cref="BudgetExhausted"/> case (no single command
/// caused it). <see cref="IsBudgetExhausted"/> is the disambiguator.
/// </param>
/// <param name="Error">
/// The engine's own rejection detail for <see cref="Rejected"/>, or
/// <see langword="null"/> for <see cref="BudgetExhausted"/>.
/// </param>
public sealed record AiTurnFailure(PlayerId Player, GameCommand? Command, GameError? Error)
{
    /// <summary>A bot's command was rejected by the engine — a bot defect, surfaced verbatim.</summary>
    public static AiTurnFailure Rejected(PlayerId player, GameCommand command, GameError error) =>
        new(player, command, error);

    /// <summary>The per-drain command budget ran out before the bot's turn(s) resolved.</summary>
    public static AiTurnFailure BudgetExhausted(PlayerId player) => new(player, null, null);

    /// <summary>
    /// <see langword="true"/> for a <see cref="BudgetExhausted"/> failure,
    /// <see langword="false"/> for a <see cref="Rejected"/> one. No single
    /// command exists to blame for a budget exhaustion, which is exactly
    /// what distinguishes the two cases here.
    /// </summary>
    public bool IsBudgetExhausted => Command is null;
}
