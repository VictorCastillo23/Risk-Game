using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Engine.Modes;

/// <summary>
/// <see cref="GameMode.Classic"/>'s victory rule: reports the first
/// non-eliminated player who owns every territory on the board. Follows the
/// same "check all active players, not just the actor" precedent established
/// by <see cref="SecretMissionVictoryRule"/> in item 1.2, applied to
/// classic conquest ("own the whole map") rather than mission cards.
/// </summary>
public sealed class ConquestVictoryRule : IVictoryRule
{
    public PlayerId? CheckVictory(GameState state)
    {
        var territoryCount = WorldMap.Territories.Count;

        foreach (var player in state.Players)
        {
            if (player.IsEliminated)
            {
                continue;
            }

            var owned = state.Territories.Values.Count(t => t.Owner == player.Id);
            if (owned == territoryCount)
            {
                return player.Id;
            }
        }

        return null;
    }
}
