using Risk.Domain.Players;

namespace Risk.Web.Models;

/// <summary>
/// Pure territory-to-owner color lookup for <c>BoardSvg</c>. Today every
/// territory is owned from the moment <c>GameSetup.Create</c> deals the
/// board (roadmap item 2.1 will introduce a Claim phase that leaves
/// territories unowned until picked — see <see cref="UnclaimedColor"/>), so
/// the unknown-owner branch below is defensive rather than reachable in
/// normal play — it keeps rendering safe if a <see cref="PlayerConfig"/> is
/// ever missing for a <see cref="PlayerId"/> present in
/// <c>GameState.Territories</c>.
/// </summary>
public static class BoardColors
{
    /// <summary>Neutral gray shown when no <see cref="PlayerConfig"/> is registered for the owner.</summary>
    public const string UnknownOwnerColor = "#9CA3AF";

    /// <summary>
    /// Distinct color for a territory with no owner at all (<c>Owner == null</c>).
    /// Kept visually distinct from <see cref="UnknownOwnerColor"/> — "nobody owns
    /// this yet" is a different situation from "someone owns this but has no
    /// registered <see cref="PlayerConfig"/>", and the two will need to diverge
    /// further once the Claim phase (roadmap item 2.1) is reachable.
    /// </summary>
    public const string UnclaimedColor = "#4B5563";

    /// <summary>
    /// Warm brown for the <see cref="GameMode.TwoPlayer"/> neutral army's
    /// territories (roadmap item 4.4) — distinct in hue and lightness from
    /// the cool grays <see cref="UnknownOwnerColor"/>/<see cref="UnclaimedColor"/>
    /// and from every swatch in <see cref="PlayerPalette.Swatches"/>.
    /// </summary>
    public const string NeutralColor = "#6D4C41";

    /// <summary>The configured color for <paramref name="owner"/>, or <see cref="UnknownOwnerColor"/> if unregistered.</summary>
    public static string OwnerColor(PlayerId owner, IReadOnlyDictionary<PlayerId, PlayerConfig> players) =>
        players.TryGetValue(owner, out var config) ? config.ColorHex : UnknownOwnerColor;
}
