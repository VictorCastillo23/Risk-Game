using Risk.Engine.State;

namespace Risk.Web.Models;

/// <summary>
/// Web-side detection of <see cref="GameMode.TwoPlayer"/>'s Setup Phase B
/// (design D1/D4) — deliberately kept to detection only, never to legality
/// (see <see cref="IsPhaseB"/>).
/// </summary>
public static class TwoPlayerSetupPhase
{
    /// <summary>
    /// Mirrors <c>GameEngine.IsPhaseB</c> (<c>src/Risk.Engine/GameEngine.cs:200-204</c>)
    /// verbatim: true once both real humans have exhausted their own Setup
    /// pool while the neutral player still has troops of its own left to
    /// place. This is a duplicated derived-state check, not a rule — it only
    /// decides which command <c>Game.razor</c> dispatches
    /// (<see cref="Risk.Engine.Commands.PlaceNeutralTroopsCommand"/> vs
    /// <see cref="Risk.Engine.Commands.PlaceTroopsCommand"/>); the engine
    /// re-validates independently regardless of the verdict here, so a wrong
    /// answer degrades to a confusing rejection, never an illegal state
    /// (design D1). Guarded against drift by
    /// <c>Risk.Web.Tests.Services.TwoPlayerSetupPhaseParityTests</c>, a
    /// bidirectional oracle against the real engine.
    /// </summary>
    public static bool IsPhaseB(GameState state) =>
        state.Mode == GameMode.TwoPlayer
        && state.Turn.Phase == TurnPhase.Setup
        && state.Players.Where(p => !p.IsNeutral).All(p => p.TroopsRemaining == 0)
        && state.Players.Single(p => p.IsNeutral).TroopsRemaining > 0;
}
