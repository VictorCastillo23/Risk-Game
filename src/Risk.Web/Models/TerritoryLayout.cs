using Risk.Domain.Map;

namespace Risk.Web.Models;

/// <summary>
/// Hand-placed pixel-coordinate layout for all 42 <see cref="WorldMap"/>
/// territories and the 6 continent bonus-label anchors, positioned directly
/// against the real watercolor map artwork (<c>wwwroot/images/world-map.png</c>,
/// 1340x876). Unlike the previous hex-grid silhouette this is not derived
/// geometry — every coordinate is a hand-authored point chosen to sit inside
/// its territory's own painted region in the artwork; presentation-only,
/// kept separate from <see cref="Risk.Domain"/>'s adjacency graph.
/// </summary>
public static class TerritoryLayout
{
    /// <summary>Width of the map artwork these coordinates are laid out on.</summary>
    public const double CanvasWidth = 1340;

    /// <summary>Height of the map artwork these coordinates are laid out on.</summary>
    public const double CanvasHeight = 876;

    /// <summary>Default radius of a territory marker, in canvas units. 2*R scaled by the
    /// reference board width (~1032 CSS px / 1340) clears WCAG 2.5.8's 24x24 px floor.</summary>
    public const double MarkerRadius = 17;

    /// <summary>Hard floor for a <see cref="RadiusOverrides"/> entry, in canvas units (~20 CSS px). See design D9.</summary>
    public const double MinRadius = 13;

    // One entry per WorldMap.Territories id: which continent it belongs to
    // (kept as its own column rather than derived from WorldMap so the seed
    // stays readable grouped per continent while authoring against the
    // artwork, and so ContinentOf_...MatchingItsRealContinent stays a real
    // cross-check instead of a tautology — design D3), and its hand-placed
    // pixel coordinate in the artwork's own 1340x876 space. Every value is
    // an integral pixel literal (design D7) so Razor's culture-aware double
    // formatting can never corrupt an SVG attribute.
    private static readonly (string Name, string ContinentId, double X, double Y)[] TerritorySeed =
    [
        // North America
        ("Alaska", "NA", 90, 140),
        ("NorthwestTerritory", "NA", 230, 130),
        ("Greenland", "NA", 505, 110),
        ("Alberta", "NA", 200, 210),
        ("Ontario", "NA", 300, 210),
        ("Quebec", "NA", 400, 190),
        ("WesternUnitedStates", "NA", 230, 310),
        ("EasternUnitedStates", "NA", 350, 325),
        ("CentralAmerica", "NA", 250, 425),

        // South America
        ("Venezuela", "SA", 355, 505),
        ("Brazil", "SA", 430, 600),
        ("Peru", "SA", 340, 630),
        ("Argentina", "SA", 395, 765),

        // Europe
        ("Iceland", "EU", 585, 195),
        ("GreatBritain", "EU", 585, 300),
        ("Scandinavia", "EU", 760, 185),
        ("NorthernEurope", "EU", 680, 310),
        ("WesternEurope", "EU", 615, 400),
        ("SouthernEurope", "EU", 700, 420),
        ("Ukraine", "EU", 820, 330),

        // Africa
        ("NorthAfrica", "AF", 630, 540),
        ("Egypt", "AF", 730, 525),
        ("EastAfrica", "AF", 780, 650),
        ("Congo", "AF", 705, 675),
        ("SouthAfrica", "AF", 705, 790),
        ("Madagascar", "AF", 845, 770),

        // Asia
        ("Ural", "AS", 960, 175),
        ("Siberia", "AS", 1090, 140),
        ("Yakutsk", "AS", 1200, 110),
        ("Kamchatka", "AS", 1210, 215),
        ("Irkutsk", "AS", 1060, 285),
        ("Mongolia", "AS", 1050, 375),
        ("Japan", "AS", 1225, 360),
        ("China", "AS", 1060, 445),
        ("Afghanistan", "AS", 920, 425),
        ("MiddleEast", "AS", 880, 495),
        ("India", "AS", 985, 540),
        ("Siam", "AS", 1075, 530),

        // Oceania
        ("Indonesia", "OC", 1065, 650),
        ("NewGuinea", "OC", 1200, 630),
        ("WesternAustralia", "OC", 1155, 770),
        ("EasternAustralia", "OC", 1240, 770)
    ];

    // One hand-placed label anchor per continent — the centroid of that
    // continent's own territory coordinates, verified against Coordinates
    // by ContinentLabelAnchors_EachSitsNearItsOwnContinent rather than
    // derived from a bounding box, since real continent bounding boxes
    // overlap (design D1, "Deviation from spec wording").
    private static readonly (string ContinentId, double X, double Y)[] ContinentLabelSeed =
    [
        ("NA", 284, 228),
        ("SA", 380, 625),
        ("EU", 678, 306),
        ("AF", 732, 658),
        ("AS", 1060, 341),
        ("OC", 1165, 705)
    ];

    /// <summary>Named exceptions only (design D9). Empty unless hand-placement proves the
    /// spacing test unsatisfiable; an entry below 16 owes an inline SC 2.5.8 justification.</summary>
    private static readonly IReadOnlyDictionary<TerritoryId, double> RadiusOverrides =
        new Dictionary<TerritoryId, double>();

    /// <summary>
    /// Named exceptions only, same shape as <see cref="RadiusOverrides"/> but for
    /// outer-ring ink color instead of radius. The first 5 entries were populated
    /// from a one-time pixel measurement against the shipped <c>world-map.png</c>
    /// artwork (risk-web-real-map-art remediation, sdd-verify CRITICAL-1): these
    /// territories sit on local art dark enough that the default
    /// <see cref="MarkerInk.Outer"/> ink fails WCAG 1.4.11's 3:1 floor against it —
    /// all 5 are small islands or coastal peninsulas surrounded by dark ocean/coastal
    /// shading. <see cref="MarkerInk.OuterOnDarkArt"/> clears 3:1 against every one
    /// of them with comfortable margin.
    ///
    /// Congo, EasternAustralia, and Argentina were added in a second remediation
    /// pass, after sdd-verify's follow-up 216-sample pixel-sampling method found
    /// these 3 sitting right at the 3:1 contrast boundary against the default
    /// outer ring, with inconsistent results across sampling parameterizations
    /// (unlike the original 5, which failed consistently every time). This is a
    /// precautionary inclusion for a borderline result, not a proven violation
    /// like the first 5.
    /// </summary>
    private static readonly IReadOnlySet<TerritoryId> LightOuterRingTerritories = new HashSet<TerritoryId>
    {
        new("Kamchatka"),
        new("Japan"),
        new("Indonesia"),
        new("NewGuinea"),
        new("Madagascar"),
        new("Congo"),
        new("EasternAustralia"),
        new("Argentina"),
    };

    /// <summary>Center point of every territory's marker, in canvas pixel coordinates.</summary>
    public static IReadOnlyDictionary<TerritoryId, (double X, double Y)> Coordinates { get; } = BuildCoordinates();

    /// <summary>The continent each territory belongs to, mirrored from <see cref="WorldMap"/> for fast lookup by the board renderer.</summary>
    public static IReadOnlyDictionary<TerritoryId, ContinentId> ContinentOf { get; } = BuildContinentOf();

    /// <summary>Hand-placed "+bonus" label anchor per continent (design D1).</summary>
    public static IReadOnlyDictionary<ContinentId, (double X, double Y)> ContinentLabelAnchors { get; } = BuildContinentLabelAnchors();

    /// <summary>Marker radius for <paramref name="territory"/>, in canvas units — <see cref="RadiusOverrides"/>'s entry if one exists, else <see cref="MarkerRadius"/>.</summary>
    public static double RadiusOf(TerritoryId territory) =>
        RadiusOverrides.TryGetValue(territory, out var radius) ? radius : MarkerRadius;

    /// <summary>
    /// True when <paramref name="territory"/>'s outer marker ring must use
    /// <see cref="MarkerInk.OuterOnDarkArt"/> instead of <see cref="MarkerInk.Outer"/>
    /// to clear WCAG 1.4.11's 3:1 floor against its own local artwork.
    /// </summary>
    public static bool NeedsLightOuterRing(TerritoryId territory) =>
        LightOuterRingTerritories.Contains(territory);

    private static IReadOnlyDictionary<TerritoryId, (double X, double Y)> BuildCoordinates()
    {
        var coordinates = new Dictionary<TerritoryId, (double X, double Y)>(TerritorySeed.Length);

        foreach (var (name, _, x, y) in TerritorySeed)
        {
            coordinates.Add(new TerritoryId(name), (x, y));
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

    private static IReadOnlyDictionary<ContinentId, (double X, double Y)> BuildContinentLabelAnchors()
    {
        var anchors = new Dictionary<ContinentId, (double X, double Y)>(ContinentLabelSeed.Length);

        foreach (var (continentId, x, y) in ContinentLabelSeed)
        {
            anchors.Add(new ContinentId(continentId), (x, y));
        }

        return anchors;
    }
}
