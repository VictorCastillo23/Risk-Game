using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Engine.Modes;

/// <summary>
/// Checks whether <paramref name="state"/> already has a winner for one
/// <see cref="GameMode"/>, returning that player's id or <see langword="null"/>
/// if the game continues.
/// </summary>
/// <remarks>
/// Invoked from two call sites in <c>GameEngine</c>, which together leave no
/// gap: <c>ExecuteAttack</c> checks immediately after a conquest flips
/// ownership (the conquered territory still holds 0 troops there), and
/// <c>ExecuteOccupy</c> checks again once <c>OccupyCommand</c> has set that
/// territory's troop count. Ownership- and elimination-based rules are decided
/// by the first call; troop-gated rules (SecretMission's
/// <c>OccupyTerritories</c> missions) are decided by the second. Implementations
/// must therefore be pure and idempotent over the state passed in — they are
/// called twice per conquest and must not assume either position.
/// </remarks>
public interface IVictoryRule
{
    PlayerId? CheckVictory(GameState state);
}
