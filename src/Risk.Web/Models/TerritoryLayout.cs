using Risk.Domain.Map;

namespace Risk.Web.Models;

/// <summary>
/// Schematic (non-geographic) board coordinates for all 42 <see cref="WorldMap"/>
/// territories, used by <c>BoardSvg</c> to render an abstract SVG board (design
/// D6). Territories are grouped into visually distinct clusters per continent
/// on a fixed <see cref="CanvasWidth"/> x <see cref="CanvasHeight"/> canvas —
/// this is presentation data, not real cartography, per the confirmed
/// non-goal of geographic accuracy.
/// </summary>
public static class TerritoryLayout
{
    /// <summary>Width of the abstract canvas these coordinates are laid out on.</summary>
    public const double CanvasWidth = 1000;

    /// <summary>Height of the abstract canvas these coordinates are laid out on.</summary>
    public const double CanvasHeight = 520;

    // One entry per WorldMap.Territories id, grouped by continent cluster.
    // Coordinates are schematic grid positions inside each continent's
    // reserved region of the canvas — not real geography.
    private static readonly (string Name, double X, double Y)[] PositionSeed =
    [
        // North America cluster (top-left)
        ("Alaska", 60, 60),
        ("NorthwestTerritory", 160, 60),
        ("Greenland", 260, 60),
        ("Alberta", 60, 160),
        ("Ontario", 160, 160),
        ("Quebec", 260, 160),
        ("WesternUnitedStates", 60, 260),
        ("EasternUnitedStates", 160, 260),
        ("CentralAmerica", 260, 260),

        // South America cluster (bottom-left)
        ("Venezuela", 80, 320),
        ("Brazil", 180, 320),
        ("Peru", 80, 420),
        ("Argentina", 180, 420),

        // Europe cluster (top-middle)
        ("Iceland", 380, 60),
        ("GreatBritain", 480, 60),
        ("Scandinavia", 580, 60),
        ("NorthernEurope", 380, 160),
        ("WesternEurope", 480, 160),
        ("SouthernEurope", 580, 160),
        ("Ukraine", 380, 260),

        // Africa cluster (bottom-middle)
        ("NorthAfrica", 400, 320),
        ("Egypt", 500, 320),
        ("EastAfrica", 600, 320),
        ("Congo", 400, 420),
        ("SouthAfrica", 500, 420),
        ("Madagascar", 600, 420),

        // Asia cluster (top-right, largest continent)
        ("Ural", 650, 60),
        ("Siberia", 750, 60),
        ("Yakutsk", 850, 60),
        ("Kamchatka", 950, 60),
        ("Irkutsk", 650, 160),
        ("Mongolia", 750, 160),
        ("Japan", 850, 160),
        ("China", 950, 160),
        ("Afghanistan", 650, 260),
        ("MiddleEast", 750, 260),
        ("India", 850, 260),
        ("Siam", 950, 260),

        // Oceania cluster (bottom-right)
        ("Indonesia", 750, 340),
        ("NewGuinea", 850, 340),
        ("WesternAustralia", 750, 440),
        ("EasternAustralia", 850, 440)
    ];

    /// <summary>Abstract (X, Y) canvas position for every territory on the board.</summary>
    public static IReadOnlyDictionary<TerritoryId, (double X, double Y)> Coordinates { get; } = Build();

    private static IReadOnlyDictionary<TerritoryId, (double X, double Y)> Build()
    {
        var coordinates = new Dictionary<TerritoryId, (double X, double Y)>(PositionSeed.Length);

        foreach (var (name, x, y) in PositionSeed)
        {
            coordinates.Add(new TerritoryId(name), (x, y));
        }

        return coordinates;
    }
}
