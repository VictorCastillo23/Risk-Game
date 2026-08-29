using Risk.Domain.Map;

namespace Risk.Web.Models;

/// <summary>
/// Spanish display labels for <see cref="Continent"/> (mirrors
/// <see cref="PhaseDisplay"/>'s exhaustive-mapping pattern — UI copy is
/// Spanish, identifiers stay English).
/// </summary>
public static class ContinentDisplay
{
    /// <summary>The Spanish label shown for <paramref name="continent"/>.</summary>
    public static string Label(ContinentId continent) => continent.Value switch
    {
        "NA" => "América del Norte",
        "SA" => "América del Sur",
        "EU" => "Europa",
        "AF" => "África",
        "AS" => "Asia",
        "OC" => "Oceanía",
        _ => continent.Value
    };
}
