using Risk.Domain.Players;

namespace Risk.Web.Models;

/// <summary>
/// MissionPanel's hot-seat peek state (presentation only — never GameState,
/// never PlayerView, never GameSessionService). Stores WHO revealed rather
/// than a bare bool, so "re-hide when the acting player changes" is derived
/// at render time and cannot be forgotten: there is no reset to perform.
/// Mirrors CardSelection/TroopStaging (immutable value + Empty/Hidden
/// singleton).
/// </summary>
public readonly record struct MissionReveal(PlayerId? RevealedFor)
{
    public static MissionReveal Hidden => new((PlayerId?)null);

    public bool IsRevealedFor(PlayerId player) => RevealedFor == player;

    public MissionReveal Toggle(PlayerId player) =>
        IsRevealedFor(player) ? Hidden : new(player);
}
