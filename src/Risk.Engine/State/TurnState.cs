using Risk.Domain.Players;

namespace Risk.Engine.State;

/// <summary>
/// Whose turn it is, which phase of that turn is active, whether the active
/// player has conquered at least one territory this turn (drives the
/// end-of-turn card draw in <c>GameEngine.AdvanceToNextPlayer</c>), whether
/// they have already used their once-per-turn <c>FortifyCommand</c>, and
/// any conquest still awaiting an <c>OccupyCommand</c>. Both per-turn flags
/// are reset to <see langword="false"/> whenever <c>EndPhaseCommand</c>
/// rotates the turn to the next player.
/// </summary>
public sealed record TurnState(
    PlayerId CurrentPlayer,
    TurnPhase Phase,
    bool ConqueredThisTurn = false,
    bool FortifyUsed = false,
    PendingOccupation? PendingOccupation = null);
