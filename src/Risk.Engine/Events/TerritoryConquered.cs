using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>Raised when a battle round reduces a defended territory's troops to zero, transferring ownership.</summary>
public sealed record TerritoryConquered(PlayerId Conqueror, PlayerId PreviousOwner, TerritoryId Territory) : GameEvent;
