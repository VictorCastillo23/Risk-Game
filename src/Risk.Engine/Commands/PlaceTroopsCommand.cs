using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Commands;

/// <summary>Places troops on a territory the actor owns, during Setup or Reinforce.</summary>
public sealed record PlaceTroopsCommand(PlayerId Actor, TerritoryId Territory, int Troops) : GameCommand(Actor);
