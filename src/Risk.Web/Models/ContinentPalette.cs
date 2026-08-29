using Risk.Domain.Map;

namespace Risk.Web.Models;

/// <summary>
/// Muted per-continent colors for the board's continent halo/outline layer
/// (the continent-shaped-map redesign). Deliberately desaturated relative to
/// every <see cref="PlayerPalette"/> swatch — different hue AND much lower
/// saturation each — so a continent's identity never reads as a player's
/// owner color at a glance. Since <c>GameSetup.Create</c> deals every
/// territory to an owner from turn 1, there is no "unclaimed" state to fill
/// with this color: it only ever appears as a low-opacity background halo
/// and territory outline, subordinate to <see cref="BoardColors.OwnerColor"/>.
/// </summary>
public static class ContinentPalette
{
    public static readonly IReadOnlyDictionary<ContinentId, string> Colors = new Dictionary<ContinentId, string>
    {
        [new ContinentId("NA")] = "#C98A3E", // amber/ochre
        [new ContinentId("SA")] = "#2F8F8A", // teal
        [new ContinentId("EU")] = "#6E8FA3", // slate blue-gray
        [new ContinentId("AF")] = "#B4592D", // terracotta
        [new ContinentId("AS")] = "#7C8A4E", // olive/sage
        [new ContinentId("OC")] = "#8C6E8A" // plum-gray
    };

    /// <summary>The configured color for <paramref name="continent"/>, or a neutral gray fallback if unregistered.</summary>
    public static string ColorOf(ContinentId continent) =>
        Colors.TryGetValue(continent, out var hex) ? hex : "#9CA3AF";
}
