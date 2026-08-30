using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Engine.Modes;

/// <summary>
/// Builds the starting <see cref="GameState"/> for one <see cref="GameMode"/>.
/// <see cref="Setup.GameSetup.Create"/> owns player-count validation and
/// <see cref="PlayerId"/> materialization; a strategy receives only the
/// already-validated player list and starting troop pool, and stamps its own
/// <see cref="GameState.Mode"/> on the result.
/// </summary>
/// <remarks>
/// <para>
/// This contract does NOT assume setup completes atomically in one call.
/// <c>Create</c> only builds a <em>starting position</em>; whether that
/// position still requires further player interaction (e.g. turn-based troop
/// placement, or a Claim phase) is entirely encoded in the returned state's
/// <c>Turn.Phase</c> and per-player <c>TroopsRemaining</c> — read by
/// <see cref="GameEngine"/>, not by this interface. A mode may return zero
/// territories owned and a <c>Claim</c> phase just as validly as a mode that
/// deals every territory and starts in <c>Setup</c>: both satisfy the same
/// postcondition below.
/// </para>
/// <para>
/// Postcondition: <c>Create</c> returns a valid <see cref="GameState"/> for
/// this mode whose <c>Log</c> holds its setup events and whose
/// <c>Turn.Phase</c> is the phase this mode starts in. There is deliberately
/// no postcondition that every territory is owned, and no postcondition that
/// <c>Turn.Phase == TurnPhase.Setup</c>.
/// </para>
/// </remarks>
public interface ISetupStrategy
{
    GameState Create(IReadOnlyList<PlayerId> players, int startingTroops);
}
