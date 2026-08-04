using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>Raised when a player places troops on one of their territories.</summary>
public sealed record TroopsPlaced(PlayerId Player, TerritoryId Territory, int Troops) : GameEvent;
