using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Engine.Modes;

/// <summary>
/// Checks whether <paramref name="state"/> already has a winner for one
/// <see cref="GameMode"/>, returning that player's id or <see langword="null"/>
/// if the game continues.
/// </summary>
/// <remarks>
/// Invoked from <c>GameEngine.ExecuteAttack</c> immediately after a conquest
/// resolves ownership, but <em>before</em> the conquered territory receives
/// its occupying troops via <c>OccupyCommand</c> — the state passed in still
/// has the conquered territory at 0 troops. Rules that only read ownership
/// and elimination state are unaffected; rules that need occupying troop
/// counts (SecretMission's <c>OccupyTerritories</c> missions) need a second
/// call site at the end of <c>ExecuteOccupy</c>, not a change to this
/// interface.
/// </remarks>
public interface IVictoryRule
{
    PlayerId? CheckVictory(GameState state);
}
