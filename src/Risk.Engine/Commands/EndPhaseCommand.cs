using Risk.Domain.Players;

namespace Risk.Engine.Commands;

/// <summary>Ends the actor's current phase, advancing to the next phase or the next player's turn.</summary>
public sealed record EndPhaseCommand(PlayerId Actor) : GameCommand(Actor);
