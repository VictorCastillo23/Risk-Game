using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Engine.Events;

/// <summary>
/// Raised in <see cref="GameMode.Capital"/> games when a conquest's target
/// territory is some player's declared headquarters. <see cref="OriginalOwner"/>
/// is always the player who originally declared <see cref="Territory"/> as
/// their headquarters (design D1: detection scans every player's
/// <c>PlayerState.HeadquartersId</c>, not the pre-conquest territory owner),
/// so a recapture chain always reports the same original declarer, never an
/// intermediate holder. Safe to carry <see cref="Territory"/> in the public,
/// unredacted <c>GameState.Log</c> (design D2): by construction, no
/// <c>AttackCommand</c> can be submitted before every headquarters has
/// already been revealed via <see cref="HeadquartersRevealed"/>.
/// </summary>
public sealed record HeadquartersCaptured(PlayerId Attacker, PlayerId OriginalOwner, TerritoryId Territory) : GameEvent;
