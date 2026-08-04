using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Commands;

/// <summary>Moves troops between two of the actor's own connected territories, once per turn.</summary>
public sealed record FortifyCommand(PlayerId Actor, TerritoryId From, TerritoryId To, int Troops) : GameCommand(Actor);
