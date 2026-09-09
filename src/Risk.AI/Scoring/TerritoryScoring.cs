using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Views;

namespace Risk.AI.Scoring;

/// <summary>
/// Threat/adjacency facts about one territory from a single player's point
/// of view, computed from <see cref="TerritoryScoring.Facts"/>. A territory
/// with no owner is neither friendly nor hostile.
/// </summary>
internal readonly record struct TerritoryFacts(
    TerritoryId Id,
    PlayerId? Owner,
    int Troops,
    int FriendlyNeighbors,
    int HostileNeighbors,
    int HostileNeighborTroops,
    int StrongestHostileNeighborTroops);

/// <summary>
/// Generic (mission/capital-agnostic) territory threat and continent
/// scoring, computed exclusively from <see cref="PlayerView.Territories"/>
/// and <see cref="WorldMap"/> — never from hidden information.
/// </summary>
internal static class TerritoryScoring
{
    /// <summary>
    /// Neighbor/threat facts for <paramref name="id"/> as seen by
    /// <paramref name="self"/>. Every neighbor is classified as friendly
    /// (owned by <paramref name="self"/>), hostile (owned by anyone else),
    /// or neither (unclaimed).
    /// </summary>
    public static TerritoryFacts Facts(PlayerView view, PlayerId self, TerritoryId id)
    {
        var state = view.Territories[id];
        var friendlyNeighbors = 0;
        var hostileNeighbors = 0;
        var hostileNeighborTroops = 0;
        var strongestHostileNeighborTroops = 0;

        foreach (var neighborId in WorldMap.NeighborsOf(id))
        {
            if (!view.Territories.TryGetValue(neighborId, out var neighbor) || neighbor.Owner is null)
            {
                continue;
            }

            if (neighbor.Owner == self)
            {
                friendlyNeighbors++;
                continue;
            }

            hostileNeighbors++;
            hostileNeighborTroops += neighbor.Troops;
            strongestHostileNeighborTroops = Math.Max(strongestHostileNeighborTroops, neighbor.Troops);
        }

        return new TerritoryFacts(
            id,
            state.Owner,
            state.Troops,
            friendlyNeighbors,
            hostileNeighbors,
            hostileNeighborTroops,
            strongestHostileNeighborTroops);
    }

    /// <summary>Closed-form defense urgency: how many troops short of covering the combined hostile threat.</summary>
    public static int DefenseUrgency(in TerritoryFacts facts) =>
        Math.Max(0, facts.HostileNeighborTroops - facts.Troops);

    /// <summary>Every territory currently owned by <paramref name="self"/>.</summary>
    public static IReadOnlyList<TerritoryId> Owned(PlayerView view, PlayerId self) =>
        view.Territories
            .Where(kv => kv.Value.Owner == self)
            .Select(kv => kv.Key)
            .ToArray();

    /// <summary>Owned territories with at least one hostile (enemy-owned) neighbor.</summary>
    public static IReadOnlyList<TerritoryId> Frontier(PlayerView view, PlayerId self) =>
        Owned(view, self)
            .Where(id => Facts(view, self, id).HostileNeighbors > 0)
            .ToArray();

    /// <summary>
    /// Every connected component of <paramref name="self"/>'s owned
    /// territories, one breadth-first-search pass over the whole owned set —
    /// used by Fortify to find which own-territory pairs even have a
    /// friendly path between them.
    /// </summary>
    public static IReadOnlyList<IReadOnlySet<TerritoryId>> OwnComponents(PlayerView view, PlayerId self)
    {
        var owned = new HashSet<TerritoryId>(Owned(view, self));
        var visited = new HashSet<TerritoryId>();
        var components = new List<IReadOnlySet<TerritoryId>>();

        foreach (var start in owned)
        {
            if (!visited.Add(start))
            {
                continue;
            }

            var component = new HashSet<TerritoryId> { start };
            var queue = new Queue<TerritoryId>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var neighborId in WorldMap.NeighborsOf(current))
                {
                    if (owned.Contains(neighborId) && visited.Add(neighborId))
                    {
                        component.Add(neighborId);
                        queue.Enqueue(neighborId);
                    }
                }
            }

            components.Add(component);
        }

        return components;
    }

    /// <summary>
    /// How much of <paramref name="id"/>'s continent bonus is captured if
    /// <paramref name="self"/> holds <paramref name="id"/>: the continent's
    /// bonus scaled by the fraction of its members <paramref name="self"/>
    /// would then own (every other member currently owned by
    /// <paramref name="self"/>, plus <paramref name="id"/> itself).
    /// </summary>
    public static double ContinentPressure(PlayerView view, PlayerId self, TerritoryId id)
    {
        var continent = Continents.All.First(c => c.Members.Contains(id));

        var ownedOtherMembers = continent.Members.Count(member =>
            !member.Equals(id) &&
            view.Territories.TryGetValue(member, out var state) &&
            state.Owner == self);

        var projectedOwned = ownedOtherMembers + 1;

        return continent.Bonus * (double)projectedOwned / continent.Members.Count;
    }
}
