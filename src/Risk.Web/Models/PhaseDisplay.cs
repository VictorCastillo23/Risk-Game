using Risk.Engine.State;

namespace Risk.Web.Models;

/// <summary>
/// Spanish display labels for <see cref="TurnPhase"/>, used by
/// <c>PhaseIndicator</c> (design D8 — UI copy is Spanish, identifiers stay
/// English). Mirrors <c>GameErrorPresenter</c>'s exhaustive-enum-mapping
/// pattern.
/// </summary>
public static class PhaseDisplay
{
    /// <summary>
    /// The Spanish label shown for <paramref name="phase"/>. Deliberately has
    /// no <c>_</c> discard arm — that fallback is what previously let raw
    /// English enum names (e.g. "Claim") leak into the Spanish UI; an
    /// unhandled <see cref="TurnPhase"/> now produces a CS8524
    /// exhaustiveness warning at compile time instead.
    /// </summary>
    public static string Label(TurnPhase phase) => phase switch
    {
        TurnPhase.Claim => "Reclamo de territorios",
        TurnPhase.Setup => "Configuración",
        TurnPhase.Reinforce => "Refuerzo",
        TurnPhase.Attack => "Ataque",
        TurnPhase.Fortify => "Fortificación"
    };
}
