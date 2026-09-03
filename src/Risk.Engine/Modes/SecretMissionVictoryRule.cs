using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine.Rules;
using Risk.Engine.State;

namespace Risk.Engine.Modes;

/// <summary>
/// SecretMission victory rule: reports the first non-eliminated player (in
/// seat order) whose dealt <see cref="MissionCard"/> archetype is complete —
/// <see cref="OccupyTerritories"/>, <see cref="EliminateArmy"/>, or
/// <see cref="ConquerContinents"/>. A dealt <see cref="EliminateArmy"/> naming
/// the holder's own seat is evaluated as <see cref="OccupyTerritories"/>(24, 1)
/// instead (see <see cref="EffectiveMission"/>), without mutating
/// <see cref="PlayerState.Mission"/>.
/// </summary>
public sealed class SecretMissionVictoryRule : IVictoryRule
{
    public PlayerId? CheckVictory(GameState state)
    {
        foreach (var player in state.Players)
        {
            if (player.IsEliminated || player.Mission is null)
            {
                continue;
            }

            if (IsComplete(EffectiveMission(player.Id, player.Mission), player.Id, state))
            {
                return player.Id;
            }
        }

        return null;
    }

    /// <summary>
    /// Check-time only (roadmap 3.2 deferral, reglasrisk.md:84-85): a dealt
    /// EliminateArmy naming the holder's OWN seat evaluates as its printed
    /// fallback. PlayerState.Mission is never rewritten.
    /// </summary>
    private static MissionCard EffectiveMission(PlayerId player, MissionCard dealt) =>
        dealt is EliminateArmy(var army) && army.Value == player.Value
            ? new OccupyTerritories(24, MinArmiesPerTerritory: 1)
            : dealt;

    private static bool IsComplete(MissionCard mission, PlayerId player, GameState state) => mission switch
    {
        OccupyTerritories m =>
            state.Territories.Values.Count(t => t.Owner == player && t.Troops >= m.MinArmiesPerTerritory) >= m.Count,
        EliminateArmy m =>
            state.Players.Single(p => p.Id.Value == m.Army.Value).IsEliminated,
        ConquerContinents m => ConquerContinentsComplete(m, player, state),
        _ => throw new InvalidOperationException("Unreachable: unknown MissionCard archetype.")
    };

    private static bool ConquerContinentsComplete(ConquerContinents m, PlayerId player, GameState state)
    {
        var fullyOwned = Continents.All
            .Where(c => ContinentControl.IsFullyOwnedBy(c, state.Territories, player))
            .Select(c => c.Id)
            .ToHashSet();

        return m.Required.All(fullyOwned.Contains)
            && fullyOwned.Count(id => !m.Required.Contains(id)) >= m.WildcardCount;
    }
}
