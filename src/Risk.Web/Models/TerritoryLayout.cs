using Risk.Domain.Map;

namespace Risk.Web.Models;

/// <summary>
/// Low-poly hex-grid board layout for all 42 <see cref="WorldMap"/>
/// territories, used by <c>BoardSvg</c> to render a stylized continent-shaped
/// map. Each continent is its own small local axial-hex grid — only a tiny
/// hand-placed (Q, R) pair per territory in <see cref="TerritorySeed"/>,
/// positioned on the shared canvas by <see cref="ContinentOrigins"/> and
/// converted to pixel geometry by <see cref="HexGrid"/>. Territories on
/// adjacent axial cells share a polygon edge, which is what makes each
/// continent read as one contiguous landmass. This is still presentation
/// data, not real cartography — a stylized/low-poly silhouette per
/// continent, per the confirmed non-goal of geographic accuracy.
/// </summary>
public static class TerritoryLayout
{
    /// <summary>Width of the canvas these coordinates are laid out on.</summary>
    public const double CanvasWidth = 1200;

    /// <summary>Height of the canvas these coordinates are laid out on.</summary>
    public const double CanvasHeight = 760;

    private const double HexSize = 40;

    // One entry per WorldMap.Territories id: which continent it belongs to,
    // and its (Q, R) axial coordinate inside that continent's own local hex
    // grid. This is the only hand-placed geometry input — everything else
    // (pixel centers, hexagon vertices) is derived by HexGrid. The (Q, R)
    // values are chosen to trace a recognizable silhouette per continent
    // (e.g. South America as a narrow north-south column, Greenland/
    // Madagascar as detached islands), not to mirror real distances.
    private static readonly (string Name, string ContinentId, int Q, int R)[] TerritorySeed =
    [
        // North America — wide north tapering to a narrow isthmus south; Greenland a detached island to the northeast
        ("Alaska", "NA", 0, 0),
        ("NorthwestTerritory", "NA", 1, 0),
        ("Greenland", "NA", 3, -1),
        ("Alberta", "NA", 0, 1),
        ("Ontario", "NA", 1, 1),
        ("Quebec", "NA", 2, 1),
        ("WesternUnitedStates", "NA", 0, 2),
        ("EasternUnitedStates", "NA", 1, 2),
        ("CentralAmerica", "NA", 1, 3),

        // South America — narrow north-south column with an eastward bulge at Brazil
        ("Venezuela", "SA", 0, 0),
        ("Brazil", "SA", 1, 0),
        ("Peru", "SA", 0, 1),
        ("Argentina", "SA", 0, 2),

        // Europe — Iceland/Scandinavia to the north, Ukraine reaching east toward Asia
        ("Iceland", "EU", 0, 0),
        ("Scandinavia", "EU", 1, 0),
        ("GreatBritain", "EU", 0, 1),
        ("NorthernEurope", "EU", 1, 1),
        ("Ukraine", "EU", 2, 1),
        ("WesternEurope", "EU", 0, 2),
        ("SouthernEurope", "EU", 1, 2),

        // Africa — Madagascar a detached island off the east coast
        ("NorthAfrica", "AF", 0, 0),
        ("Egypt", "AF", 1, 0),
        ("Congo", "AF", 0, 1),
        ("EastAfrica", "AF", 1, 1),
        ("Madagascar", "AF", 2, 1),
        ("SouthAfrica", "AF", 0, 2),

        // Asia — the largest continent: wide north tapering to the Middle East / India peninsula.
        // Coordinates are deliberately NOT a simple grid: they trace the real WorldMap
        // adjacency graph as hex-neighbor offsets (each real border is an axial-neighbor
        // pair) while keeping non-adjacent pairs apart, so no two hexes visually touch
        // unless WorldMap.AreAdjacent agrees — see HexAdjacencyRegressionTests.
        ("Ural", "AS", -1, 0),
        ("Siberia", "AS", 0, 0),
        ("Yakutsk", "AS", 1, 0),
        ("Kamchatka", "AS", 1, 1),
        ("Irkutsk", "AS", 0, 1),
        ("Mongolia", "AS", 0, 2),
        ("Japan", "AS", 1, 2),
        ("Afghanistan", "AS", -2, 3),
        ("China", "AS", -1, 3),
        ("Siam", "AS", -1, 4),
        ("MiddleEast", "AS", -3, 4),
        ("India", "AS", -2, 4),

        // Oceania — Indonesia/New Guinea islands north of the Australian mainland
        ("Indonesia", "OC", 0, 0),
        ("NewGuinea", "OC", 1, 0),
        ("WesternAustralia", "OC", 0, 1),
        ("EasternAustralia", "OC", 1, 1)
    ];

    // Positions each continent's local hex grid on the shared canvas —
    // mirrors the previous schematic layout's macro clusters (NA top-left,
    // EU top-middle, AS top-right/largest, SA bottom-left, AF
    // bottom-middle, OC bottom-right) so the six regions stay in their
    // familiar places; only the shape within each cluster changed from
    // loose dots to a contiguous hex blob.
    private static readonly IReadOnlyDictionary<string, (double OriginX, double OriginY)> ContinentOrigins =
        new Dictionary<string, (double OriginX, double OriginY)>
        {
            ["NA"] = (90, 160),
            ["SA"] = (140, 480),
            ["EU"] = (520, 140),
            ["AF"] = (520, 460),
            ["AS"] = (900, 120),
            ["OC"] = (980, 520)
        };

    /// <summary>Hex-vertex polygon for every territory, in canvas pixel coordinates.</summary>
    public static IReadOnlyDictionary<TerritoryId, IReadOnlyList<(double X, double Y)>> Polygons { get; } = BuildPolygons();

    /// <summary>Precomputed SVG <c>points</c> attribute string per territory, so <c>BoardSvg</c> doesn't re-join coordinates on every render.</summary>
    public static IReadOnlyDictionary<TerritoryId, string> PolygonPointsAttr { get; } = BuildPolygonPointsAttr();

    /// <summary>Center point of every territory's hexagon — used for troop-count/label placement and adjacency-line endpoints.</summary>
    public static IReadOnlyDictionary<TerritoryId, (double X, double Y)> Coordinates { get; } = BuildCoordinates();

    /// <summary>The continent each territory belongs to, mirrored from <see cref="WorldMap"/> for fast lookup by the board renderer.</summary>
    public static IReadOnlyDictionary<TerritoryId, ContinentId> ContinentOf { get; } = BuildContinentOf();

    /// <summary>
    /// Padded bounding box per continent (over every member territory's hex
    /// vertices), used to draw the continent halo/label backdrop layer.
    /// </summary>
    public static IReadOnlyDictionary<ContinentId, (double X, double Y, double Width, double Height)> ContinentBounds { get; } = BuildContinentBounds();

    private static IReadOnlyDictionary<TerritoryId, IReadOnlyList<(double X, double Y)>> BuildPolygons()
    {
        var polygons = new Dictionary<TerritoryId, IReadOnlyList<(double X, double Y)>>(TerritorySeed.Length);

        foreach (var (name, continentId, q, r) in TerritorySeed)
        {
            var origin = ContinentOrigins[continentId];
            var center = HexGrid.AxialToPixel(q, r, HexSize, origin.OriginX, origin.OriginY);
            polygons.Add(new TerritoryId(name), HexGrid.Corners(center.X, center.Y, HexSize));
        }

        return polygons;
    }

    private static IReadOnlyDictionary<TerritoryId, string> BuildPolygonPointsAttr()
    {
        var attrs = new Dictionary<TerritoryId, string>(Polygons.Count);

        foreach (var (id, points) in Polygons)
        {
            attrs.Add(id, string.Join(' ', points.Select(p => $"{p.X.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)},{p.Y.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}")));
        }

        return attrs;
    }

    private static IReadOnlyDictionary<TerritoryId, (double X, double Y)> BuildCoordinates()
    {
        var coordinates = new Dictionary<TerritoryId, (double X, double Y)>(TerritorySeed.Length);

        foreach (var (name, continentId, q, r) in TerritorySeed)
        {
            var origin = ContinentOrigins[continentId];
            coordinates.Add(new TerritoryId(name), HexGrid.AxialToPixel(q, r, HexSize, origin.OriginX, origin.OriginY));
        }

        return coordinates;
    }

    private static IReadOnlyDictionary<TerritoryId, ContinentId> BuildContinentOf()
    {
        var continentOf = new Dictionary<TerritoryId, ContinentId>(TerritorySeed.Length);

        foreach (var (name, continentId, _, _) in TerritorySeed)
        {
            continentOf.Add(new TerritoryId(name), new ContinentId(continentId));
        }

        return continentOf;
    }

    private static IReadOnlyDictionary<ContinentId, (double X, double Y, double Width, double Height)> BuildContinentBounds()
    {
        const double padding = 26;
        var bounds = new Dictionary<ContinentId, (double X, double Y, double Width, double Height)>();

        foreach (var group in Polygons.GroupBy(kv => ContinentOf[kv.Key]))
        {
            var points = group.SelectMany(kv => kv.Value).ToArray();
            var minX = points.Min(p => p.X) - padding;
            var maxX = points.Max(p => p.X) + padding;
            var minY = points.Min(p => p.Y) - padding;
            var maxY = points.Max(p => p.Y) + padding;

            bounds.Add(group.Key, (minX, minY, maxX - minX, maxY - minY));
        }

        return bounds;
    }
}
