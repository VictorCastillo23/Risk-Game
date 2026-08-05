using Risk.Domain.Map;

namespace Risk.Web.Models;

/// <summary>
/// Pure derivation of <c>BoardSvg</c>'s connector lines from
/// <see cref="WorldMap"/>'s directed adjacency data. <see cref="WorldMap.NeighborsOf"/>
/// reports each pair in both directions (A→B and B→A); this collapses that
/// into one undirected edge per adjacent pair so the board renders each
/// connector line exactly once.
/// </summary>
public static class BoardEdges
{
    /// <summary>Every adjacent territory pair from <see cref="WorldMap"/>, each appearing exactly once.</summary>
    public static IReadOnlyList<(TerritoryId A, TerritoryId B)> UniqueEdges()
    {
        var seen = new HashSet<(TerritoryId, TerritoryId)>();
        var edges = new List<(TerritoryId, TerritoryId)>();

        foreach (var territory in WorldMap.Territories)
        {
            foreach (var neighbor in WorldMap.NeighborsOf(territory.Id))
            {
                var key = Normalize(territory.Id, neighbor);

                if (seen.Add(key))
                {
                    edges.Add(key);
                }
            }
        }

        return edges;
    }

    private static (TerritoryId A, TerritoryId B) Normalize(TerritoryId a, TerritoryId b) =>
        string.CompareOrdinal(a.Value, b.Value) <= 0 ? (a, b) : (b, a);
}
