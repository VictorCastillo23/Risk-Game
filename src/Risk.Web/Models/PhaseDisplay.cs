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
    /// <summary>The Spanish label shown for <paramref name="phase"/>.</summary>
    public static string Label(TurnPhase phase) => phase switch
    {
        TurnPhase.Setup => "Configuración",
        TurnPhase.Reinforce => "Refuerzo",
        TurnPhase.Attack => "Ataque",
        TurnPhase.Fortify => "Fortificación",
        _ => phase.ToString()
    };
}
