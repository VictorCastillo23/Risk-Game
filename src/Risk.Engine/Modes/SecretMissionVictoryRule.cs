using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Engine.Modes;

/// <summary>
/// Interim SecretMission victory rule: reports the first non-eliminated
/// player who owns every territory on the board. This mirrors the
/// pre-refactor actor-only conquest check, widened to check all active
/// players (see <see cref="IVictoryRule"/> remarks) — real mission-card-based
/// victory is a later roadmap item, not implemented here.
/// </summary>
public sealed class SecretMissionVictoryRule : IVictoryRule
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
