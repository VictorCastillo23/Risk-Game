namespace Risk.Web.Models;

/// <summary>
/// The host's lobby snapshot broadcast to every connected circuit:
/// who sits where, who is (re)connected, and whether the game started.
/// Immutable DTO — the session builds a fresh one per broadcast.
/// </summary>
public sealed record LobbyState(
    JoinCode Code,
    IReadOnlyList<PlayerSeat> Seats,
    bool IsStarted);
