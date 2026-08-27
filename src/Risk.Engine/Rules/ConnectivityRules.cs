using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Engine.Rules;

/// <summary>
/// Validates territory-chain connectivity for actions that require an
/// unbroken path of one player's own territories (currently Fortify).
/// </summary>
public static class ConnectivityRules
{
    /// <summary>
    /// Breadth-first search restricted to territories owned by
    /// <paramref name="owner"/>: true if <paramref name="to"/> is reachable
    /// from <paramref name="from"/> by crossing only territories that
    /// player owns (a direct edge is the trivial 2-territory case of this
    /// chain, and <paramref name="from"/> equal to <paramref name="to"/> is
    /// trivially true).
    /// </summary>
    public static bool HasFriendlyPath(
        IReadOnlyDictionary<TerritoryId, TerritoryState> territories, PlayerId owner, TerritoryId from, TerritoryId to)
    {
        var visited = new HashSet<TerritoryId> { from };
        var queue = new Queue<TerritoryId>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == to)
            {
                return true;
            }

            foreach (var neighbor in WorldMap.NeighborsOf(current))
            {
                if (visited.Contains(neighbor))
                {
                    continue;
                }

                if (!territories.TryGetValue(neighbor, out var neighborState) || neighborState.Owner != owner)
                {
                    continue;
                }

                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return false;
    }
}
