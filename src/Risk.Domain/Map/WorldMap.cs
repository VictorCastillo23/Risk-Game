using Risk.Domain.Cards;

namespace Risk.Domain.Map;

/// <summary>
/// The classic 42-territory, 6-continent Risk board: real territory names,
/// continent membership, card symbols, and the full bidirectional adjacency
/// graph, including the classic non-contiguous sea routes (Alaska-Kamchatka,
/// Greenland-Iceland, Brazil-North Africa, and others).
/// </summary>
public static class WorldMap
{
    // NOTE: seed arrays (TerritorySeed, EdgeSeed) are declared before the
    // derived static properties below because C# initializes static fields
    // in textual declaration order — Territories/Adjacency must not run
    // before the seed data they read from exists.

    // Fixed enumeration order; assigning CardSymbol by (index % 3) yields an
    // even 14/14/14 split, matching the proposal's pinned "leans even" default
    // for the unresolved classic-symbol-assignment question (see design.md
    // Open Questions).
    private static readonly (string Name, string ContinentId)[] TerritorySeed =
    [
        // North America (9)
        ("Alaska", "NA"),
        ("NorthwestTerritory", "NA"),
        ("Greenland", "NA"),
        ("Alberta", "NA"),
        ("Ontario", "NA"),
        ("Quebec", "NA"),
        ("WesternUnitedStates", "NA"),
        ("EasternUnitedStates", "NA"),
        ("CentralAmerica", "NA"),
        // South America (4)
        ("Venezuela", "SA"),
        ("Brazil", "SA"),
        ("Peru", "SA"),
        ("Argentina", "SA"),
        // Europe (7)
        ("Iceland", "EU"),
        ("GreatBritain", "EU"),
        ("Scandinavia", "EU"),
        ("NorthernEurope", "EU"),
        ("WesternEurope", "EU"),
        ("SouthernEurope", "EU"),
        ("Ukraine", "EU"),
        // Africa (6)
        ("NorthAfrica", "AF"),
        ("Egypt", "AF"),
        ("EastAfrica", "AF"),
        ("Congo", "AF"),
        ("SouthAfrica", "AF"),
        ("Madagascar", "AF"),
        // Asia (12)
        ("Ural", "AS"),
        ("Siberia", "AS"),
        ("Yakutsk", "AS"),
        ("Kamchatka", "AS"),
        ("Irkutsk", "AS"),
        ("Mongolia", "AS"),
        ("Japan", "AS"),
        ("China", "AS"),
        ("Afghanistan", "AS"),
        ("MiddleEast", "AS"),
        ("India", "AS"),
        ("Siam", "AS"),
        // Oceania (4)
        ("Indonesia", "OC"),
        ("NewGuinea", "OC"),
        ("WesternAustralia", "OC"),
        ("EasternAustralia", "OC")
    ];

    // Full classic Risk adjacency graph. Each pair listed once; both
    // directions are added when the graph is built. Comments mark the
    // non-contiguous sea routes that connect the continents.
    private static readonly (string From, string To)[] EdgeSeed =
    [
        // North America
        ("Alaska", "NorthwestTerritory"), ("Alaska", "Alberta"),
        ("NorthwestTerritory", "Alberta"), ("NorthwestTerritory", "Ontario"), ("NorthwestTerritory", "Greenland"),
        ("Greenland", "Ontario"), ("Greenland", "Quebec"),
        ("Alberta", "Ontario"), ("Alberta", "WesternUnitedStates"),
        ("Ontario", "Quebec"), ("Ontario", "WesternUnitedStates"), ("Ontario", "EasternUnitedStates"),
        ("Quebec", "EasternUnitedStates"),
        ("WesternUnitedStates", "EasternUnitedStates"), ("WesternUnitedStates", "CentralAmerica"),
        ("EasternUnitedStates", "CentralAmerica"),

        // NA <-> SA / EU / AS
        ("CentralAmerica", "Venezuela"),
        ("Alaska", "Kamchatka"),    // sea route
        ("Greenland", "Iceland"),   // sea route

        // South America
        ("Venezuela", "Brazil"), ("Venezuela", "Peru"),
        ("Brazil", "Peru"), ("Brazil", "Argentina"),
        ("Peru", "Argentina"),

        // SA <-> AF
        ("Brazil", "NorthAfrica"),  // sea route

        // Europe
        ("Iceland", "GreatBritain"), ("Iceland", "Scandinavia"),
        ("GreatBritain", "Scandinavia"), ("GreatBritain", "NorthernEurope"), ("GreatBritain", "WesternEurope"),
        ("Scandinavia", "NorthernEurope"), ("Scandinavia", "Ukraine"),
        ("NorthernEurope", "Ukraine"), ("NorthernEurope", "SouthernEurope"), ("NorthernEurope", "WesternEurope"),
        ("WesternEurope", "SouthernEurope"),
        ("SouthernEurope", "Ukraine"),

        // EU <-> AF
        ("WesternEurope", "NorthAfrica"),  // sea route
        ("SouthernEurope", "NorthAfrica"),
        ("SouthernEurope", "Egypt"),

        // EU <-> AS
        ("Ukraine", "Ural"), ("Ukraine", "Afghanistan"), ("Ukraine", "MiddleEast"),
        ("SouthernEurope", "MiddleEast"),

        // Africa
        ("NorthAfrica", "Egypt"), ("NorthAfrica", "EastAfrica"), ("NorthAfrica", "Congo"),
        ("Egypt", "EastAfrica"),
        ("EastAfrica", "Congo"), ("EastAfrica", "SouthAfrica"), ("EastAfrica", "Madagascar"),
        ("Congo", "SouthAfrica"),
        ("SouthAfrica", "Madagascar"),

        // AF <-> AS
        ("Egypt", "MiddleEast"), ("EastAfrica", "MiddleEast"),

        // Asia
        ("Ural", "Siberia"), ("Ural", "China"), ("Ural", "Afghanistan"),
        ("Siberia", "Yakutsk"), ("Siberia", "Irkutsk"), ("Siberia", "Mongolia"), ("Siberia", "China"),
        ("Yakutsk", "Irkutsk"), ("Yakutsk", "Kamchatka"),
        ("Kamchatka", "Irkutsk"), ("Kamchatka", "Mongolia"), ("Kamchatka", "Japan"),
        ("Irkutsk", "Mongolia"),
        ("Mongolia", "Japan"), ("Mongolia", "China"),
        ("China", "Afghanistan"), ("China", "India"), ("China", "Siam"),
        ("Afghanistan", "India"), ("Afghanistan", "MiddleEast"),
        ("MiddleEast", "India"),
        ("India", "Siam"),

        // AS <-> OC
        ("Siam", "Indonesia"),   // sea route

        // Oceania
        ("Indonesia", "NewGuinea"), ("Indonesia", "WesternAustralia"),
        ("NewGuinea", "WesternAustralia"), ("NewGuinea", "EasternAustralia"),
        ("WesternAustralia", "EasternAustralia")
    ];

    /// <summary>The 42 territories, each with its continent and card symbol.</summary>
    public static IReadOnlyList<Territory> Territories { get; } = BuildTerritories();

    /// <summary>True if <paramref name="a"/> and <paramref name="b"/> are directly connected.</summary>
    public static bool AreAdjacent(TerritoryId a, TerritoryId b) =>
        Adjacency.TryGetValue(a, out var neighbors) && neighbors.Contains(b);

    /// <summary>All territories directly connected to <paramref name="territory"/>.</summary>
    public static IReadOnlyCollection<TerritoryId> NeighborsOf(TerritoryId territory) =>
        Adjacency.TryGetValue(territory, out var neighbors) ? neighbors : Array.Empty<TerritoryId>();

    private static readonly IReadOnlyDictionary<TerritoryId, IReadOnlySet<TerritoryId>> Adjacency = BuildAdjacency();

    private static IReadOnlyList<Territory> BuildTerritories()
    {
        var symbols = new[] { CardSymbol.Infantry, CardSymbol.Cavalry, CardSymbol.Artillery };
        var territories = new List<Territory>(TerritorySeed.Length);

        for (var i = 0; i < TerritorySeed.Length; i++)
        {
            var (name, continentId) = TerritorySeed[i];
            territories.Add(new Territory(new TerritoryId(name), new ContinentId(continentId), symbols[i % symbols.Length]));
        }

        return territories;
    }

    private static IReadOnlyDictionary<TerritoryId, IReadOnlySet<TerritoryId>> BuildAdjacency()
    {
        var adjacency = new Dictionary<TerritoryId, HashSet<TerritoryId>>();

        foreach (var (from, to) in EdgeSeed)
        {
            var fromId = new TerritoryId(from);
            var toId = new TerritoryId(to);
            AddDirectedEdge(adjacency, fromId, toId);
            AddDirectedEdge(adjacency, toId, fromId);
        }

        return adjacency.ToDictionary(kv => kv.Key, kv => (IReadOnlySet<TerritoryId>)kv.Value);
    }

    private static void AddDirectedEdge(Dictionary<TerritoryId, HashSet<TerritoryId>> adjacency, TerritoryId from, TerritoryId to)
    {
        if (!adjacency.TryGetValue(from, out var neighbors))
        {
            neighbors = [];
            adjacency[from] = neighbors;
        }

        neighbors.Add(to);
    }
}
