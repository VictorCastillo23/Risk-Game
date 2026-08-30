using Risk.Engine.State;

namespace Risk.Web.Models;

/// <summary>
/// Spanish display labels for <see cref="GameMode"/>, used by the setup
/// screen's mode selector (design D8 — UI copy is Spanish, identifiers stay
/// English). Mirrors <c>PhaseDisplay</c>'s exhaustive-enum-mapping pattern.
/// </summary>
public static class GameModeDisplay
{
    /// <summary>
    /// The Spanish label shown for <paramref name="mode"/>. All four modes
    /// are mapped even though only <see cref="GameMode.Classic"/> is
    /// currently selectable in <c>Setup.razor</c>, so the switch stays
    /// exhaustive as later roadmap items add more selectable modes.
    /// Deliberately has no <c>_</c> discard arm — an unhandled
    /// <see cref="GameMode"/> produces a CS8524 exhaustiveness warning at
    /// compile time instead of leaking a raw English enum name.
    /// </summary>
    public static string Label(GameMode mode) => mode switch
    {
        GameMode.Classic => "Clásico",
        GameMode.SecretMission => "Misión secreta",
        GameMode.TwoPlayer => "Dos jugadores",
        GameMode.Capital => "Capital"
    };
}
