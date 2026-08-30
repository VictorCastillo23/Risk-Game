using Risk.Domain.Players;

namespace Risk.Engine.State;

/// <summary>
/// Whose turn it is, which phase of that turn is active, whether the active
/// player has conquered at least one territory this turn (drives the
/// conquest card draw in <c>GameEngine.AdvanceFromAttackToFortify</c> at the
/// Attack → Fortify transition), whether they have already used their
/// once-per-turn <c>FortifyCommand</c>, any conquest still awaiting an
/// <c>OccupyCommand</c>, and whether an elimination has armed a mandatory
/// overflow trade-down. <c>ConqueredThisTurn</c> is reset to
/// <see langword="false"/> at the Attack → Fortify transition; every
/// per-turn flag (including <c>MandatoryTradeDown</c>) is reset again via a
/// fresh <c>TurnState</c> whenever <c>EndPhaseCommand</c> rotates the turn to
/// the next player.
/// </summary>
/// <remarks>
/// <c>MandatoryTradeDown</c> is armed in <c>GameEngine.ExecuteAttack</c> when
/// eliminating a player leaves the eliminator holding 6 or more cards
/// (landing at exactly 5 does not arm it — that hand instead carries into
/// the eliminator's next Reinforce phase, where the turn-start mandatory
/// trade-in rule applies on its own). It is cleared in
/// <c>GameEngine.ExecuteTradeCards</c> only once a trade leaves the actor
/// with 4 or fewer cards; a trade that still leaves 5+ cards keeps the flag
/// armed so <c>Execute</c>'s mandatory-trade gate keeps blocking non-trade
/// commands through a multi-trade overflow sequence. The flag cannot survive
/// a turn or phase rotation: while armed with 5+ cards it blocks
/// <c>EndPhaseCommand</c> itself, and both <c>AdvanceToNextPlayer</c> and
/// <c>AdvanceAfterSetupPlacement</c> construct a brand new <c>TurnState</c>
/// (defaulting this flag back to <see langword="false"/>) rather than
/// mutating the existing one.
/// </remarks>
public sealed record TurnState(
    PlayerId CurrentPlayer,
    TurnPhase Phase,
    bool ConqueredThisTurn = false,
    bool FortifyUsed = false,
    PendingOccupation? PendingOccupation = null,
    bool MandatoryTradeDown = false);
