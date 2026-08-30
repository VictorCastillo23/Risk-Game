using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Commands;

/// <summary>Claims an unowned territory during <see cref="Risk.Engine.State.TurnPhase.Claim"/>, consuming troops from the actor's starting pool.</summary>
public sealed record ClaimTerritoryCommand(PlayerId Actor, TerritoryId Territory, int Troops) : GameCommand(Actor);
