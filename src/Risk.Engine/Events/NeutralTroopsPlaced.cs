using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>
/// Raised when a human places one of the neutral player's troops on a
/// neutral-owned territory during <see cref="Risk.Engine.State.GameMode.TwoPlayer"/>'s
/// Setup Phase B (roadmap 4.1). <see cref="Placer"/> is the deciding human,
/// never the neutral itself — the neutral is a board object, not an agent.
/// </summary>
public sealed record NeutralTroopsPlaced(PlayerId Placer, TerritoryId Territory, int Troops) : GameEvent;
