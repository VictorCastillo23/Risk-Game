using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>Raised when a <c>FortifyCommand</c> moves troops between two of the actor's connected territories.</summary>
public sealed record TroopsFortified(PlayerId Actor, TerritoryId From, TerritoryId To, int Troops) : GameEvent;
