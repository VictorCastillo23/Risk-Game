using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>Raised after one battle round's dice are compared and losses are applied.</summary>
public sealed record BattleResolved(
    PlayerId Attacker,
    TerritoryId From,
    TerritoryId To,
    IReadOnlyList<int> AttackerRolls,
    IReadOnlyList<int> DefenderRolls,
    int AttackerLosses,
    int DefenderLosses) : GameEvent;
