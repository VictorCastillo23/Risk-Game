using Risk.Domain.Players;

namespace Risk.Web.Models;

/// <summary>
/// Pure territory-to-owner color lookup for <c>BoardSvg</c>. Every territory
/// is owned from the moment <c>GameSetup.Create</c> deals the board, so the
/// unknown-owner branch is defensive rather than reachable in normal play —
/// it keeps rendering safe if a <see cref="PlayerConfig"/> is ever missing
/// for a <see cref="PlayerId"/> present in <c>GameState.Territories</c>.
/// </summary>
public static class BoardColors
{
    /// <summary>Neutral gray shown when no <see cref="PlayerConfig"/> is registered for the owner.</summary>
    public const string UnknownOwnerColor = "#9CA3AF";

    /// <summary>The configured color for <paramref name="owner"/>, or <see cref="UnknownOwnerColor"/> if unregistered.</summary>
    public static string OwnerColor(PlayerId owner, IReadOnlyDictionary<PlayerId, PlayerConfig> players) =>
        players.TryGetValue(owner, out var config) ? config.ColorHex : UnknownOwnerColor;
}
