using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Commands;

/// <summary>
/// Designates one territory owned by the actor as their headquarters during
/// <see cref="Risk.Engine.State.TurnPhase.SelectHeadquarters"/>. Ownership is
/// the only constraint — no continent or adjacency rule applies.
/// </summary>
public sealed record SelectHeadquartersCommand(PlayerId Actor, TerritoryId Territory) : GameCommand(Actor);
