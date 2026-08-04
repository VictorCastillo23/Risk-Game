using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>Raised once, at setup, when all 42 territories are dealt to players.</summary>
public sealed record TerritoriesAssigned(IReadOnlyDictionary<TerritoryId, PlayerId> Assignments) : GameEvent;
