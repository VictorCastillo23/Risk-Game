using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Engine.Rules;

/// <summary>
/// Computes how many reinforcement troops a player receives at the start of
/// their Reinforce phase: floor(owned territories / 3), minimum 3, plus the
/// bonus for every continent the player fully controls.
/// </summary>
public static class Reinforcement
{
    private const int MinimumTroops = 3;
    private const int TerritoriesPerTroop = 3;

    public static int Calculate(IReadOnlyDictionary<TerritoryId, TerritoryState> territories, PlayerId player)
    {
        var owned = territories.Values.Count(t => t.Owner == player);
        var territoryTroops = Math.Max(owned / TerritoriesPerTroop, MinimumTroops);

        var continentBonus = Continents.All
            .Where(c => c.Members.Count > 0 && c.Members.All(m => territories.TryGetValue(m, out var t) && t.Owner == player))
            .Sum(c => c.Bonus);

        return territoryTroops + continentBonus;
    }
}
