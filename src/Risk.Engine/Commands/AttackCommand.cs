using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Commands;

/// <summary>Attacks an adjacent enemy territory using up to <see cref="DiceCount"/> dice.</summary>
public sealed record AttackCommand(PlayerId Actor, TerritoryId From, TerritoryId To, int DiceCount) : GameCommand(Actor);
