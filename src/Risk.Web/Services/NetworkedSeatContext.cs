using Risk.Domain.Players;
using Risk.Web.Models;

namespace Risk.Web.Services;

/// <summary>
/// This circuit's networked-game identity: which game (by join code) and
/// which seat it owns. Scoped = one per Blazor circuit = one device.
/// Set once when the circuit creates or joins a game; read by Lobby/Game
/// to decide host vs joiner rendering and turn gating. Nulls = this
/// circuit is not in any networked game (plain hot-seat browsing).
/// Mutable holder, no behavior — exercised through the UI flow.
/// </summary>
public sealed class NetworkedSeatContext
{
    /// <summary>Blazor circuit id, captured by <see cref="NetworkCircuitHandler"/>.</summary>
    public string? CircuitId { get; set; }

    public JoinCode? Code { get; set; }

    public PlayerId? Seat { get; set; }
}
