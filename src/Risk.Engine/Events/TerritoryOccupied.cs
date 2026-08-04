using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>Raised when an <c>OccupyCommand</c> resolves a pending conquest, moving troops into the new territory.</summary>
public sealed record TerritoryOccupied(PlayerId Player, TerritoryId Territory, int Troops) : GameEvent;
