using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Commands;

/// <summary>
/// Places one of the neutral player's troops on a neutral-owned territory,
/// during <see cref="Risk.Engine.State.GameMode.TwoPlayer"/>'s Setup Phase B
/// (roadmap 4.1). <see cref="GameCommand.Actor"/> must be one of the two real
/// humans — never the neutral's own <see cref="PlayerId"/> — since the
/// neutral never becomes <see cref="Risk.Engine.State.TurnState.CurrentPlayer"/>.
/// </summary>
public sealed record PlaceNeutralTroopsCommand(PlayerId Actor, TerritoryId Territory, int Troops) : GameCommand(Actor);
