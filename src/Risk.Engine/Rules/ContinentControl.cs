using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Engine.Rules;

/// <summary>
/// Determines whether a single player fully owns every member territory of a
/// continent. Shared by <see cref="Reinforcement"/> (continent bonus) and the
/// SecretMission <c>ConquerContinents</c> victory check.
/// </summary>
public static class ContinentControl
{
    public static bool IsFullyOwnedBy(
        Continent continent,
        IReadOnlyDictionary<TerritoryId, TerritoryState> territories,
        PlayerId player) =>
        continent.Members.Count > 0
        && continent.Members.All(m => territories.TryGetValue(m, out var t) && t.Owner == player);
}
