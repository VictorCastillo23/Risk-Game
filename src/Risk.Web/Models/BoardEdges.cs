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

    // The 5 classic non-contiguous sea routes, per WorldMap.EdgeSeed's own
    // "// sea route" comments. Hardcoded rather than derived from "different
    // ContinentId", since several land borders also cross continents
    // (Ukraine-Ural, Ukraine-Afghanistan, Egypt-MiddleEast, etc.).
    private static readonly IReadOnlySet<(TerritoryId A, TerritoryId B)> SeaRoutes = new HashSet<(TerritoryId, TerritoryId)>
    {
        Normalize(new TerritoryId("Alaska"), new TerritoryId("Kamchatka")),
        Normalize(new TerritoryId("Greenland"), new TerritoryId("Iceland")),
        Normalize(new TerritoryId("Brazil"), new TerritoryId("NorthAfrica")),
        Normalize(new TerritoryId("WesternEurope"), new TerritoryId("NorthAfrica")),
        Normalize(new TerritoryId("Siam"), new TerritoryId("Indonesia"))
    };

    /// <summary>True if the edge between <paramref name="a"/> and <paramref name="b"/> is one of the classic non-contiguous sea routes.</summary>
    public static bool IsSeaRoute(TerritoryId a, TerritoryId b) => SeaRoutes.Contains(Normalize(a, b));

    private static (TerritoryId A, TerritoryId B) Normalize(TerritoryId a, TerritoryId b) =>
        string.CompareOrdinal(a.Value, b.Value) <= 0 ? (a, b) : (b, a);
}
