using Risk.Domain.Players;

namespace Risk.Engine.State;

/// <summary>
/// Whose turn it is, which phase of that turn is active, whether the active
/// player has conquered at least one territory this turn (drives the
/// end-of-turn card draw, awarded in a later PR), and any conquest still
/// awaiting an <c>OccupyCommand</c>.
/// </summary>
public sealed record TurnState(
    PlayerId CurrentPlayer,
    TurnPhase Phase,
    bool ConqueredThisTurn = false,
    PendingOccupation? PendingOccupation = null);
