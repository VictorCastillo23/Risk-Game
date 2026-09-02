using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>
/// Raised when a player selects their headquarters territory. Deliberately
/// carries no territory — <c>GameState.Log</c> is public/unredacted, so
/// including the territory here would leak the secret to every player before
/// the reveal (see <see cref="HeadquartersRevealed"/>).
/// </summary>
public sealed record HeadquartersSelected(PlayerId Player) : GameEvent;
