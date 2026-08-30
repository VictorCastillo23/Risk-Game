using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>Raised when a player claims a previously unowned territory.</summary>
public sealed record TerritoryClaimed(PlayerId Player, TerritoryId Territory, int Troops) : GameEvent;
