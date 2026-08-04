using Risk.Domain.Players;

namespace Risk.Engine.Commands;

/// <summary>Resolves a pending conquest by moving troops into the newly-conquered territory.</summary>
public sealed record OccupyCommand(PlayerId Actor, int Troops) : GameCommand(Actor);
