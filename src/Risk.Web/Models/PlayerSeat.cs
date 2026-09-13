namespace Risk.Web.Models;

/// <summary>
/// A networked-game seat: the hot-seat <see cref="PlayerConfig"/> plus the
/// connection binding that tells the server which circuit owns it.
/// <see cref="ConnectionId"/> is the Blazor circuit id (null = AI seat or
/// not yet connected). No behavior — identity data only, exercised through
/// the session/hub tests in Phase 1.
/// </summary>
public sealed record PlayerSeat(
    PlayerConfig Config,
    string? ConnectionId,
    bool IsHost,
    bool IsConnected,
    bool IsSpectator);
