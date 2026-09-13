using Risk.Domain.Errors;
using Risk.Domain.Players;
using Risk.Engine.Commands;

namespace Risk.Web.Services;

/// <summary>
/// A bot turn that stopped the table instead of passing play on: either
/// the engine rejected a bot command (<see cref="Rejected"/> — a defect
/// per Risk.AI's zero-rejected invariant, surfaced never masked) or the
/// per-turn command budget ran out (<see cref="BudgetExhausted"/>).
/// Null while every bot turn completes cleanly.
/// </summary>
public sealed record AiTurnFailure(PlayerId Player, GameCommand? Command, GameError? Error)
{
    public bool IsBudgetExhausted => Command is null;

    public static AiTurnFailure Rejected(PlayerId player, GameCommand command, GameError error) =>
        new(player, command, error);

    public static AiTurnFailure BudgetExhausted(PlayerId player) =>
        new(player, null, null);
}
