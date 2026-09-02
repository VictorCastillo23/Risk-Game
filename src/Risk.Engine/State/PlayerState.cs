using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.State;

/// <summary>
/// A player's game state: their hand, whether they're eliminated, and any
/// troops they still need to place (starting troops during Setup, or the
/// current turn's reinforcement during Reinforce). <see cref="HeadquartersId"/>
/// is write-once, set during <see cref="TurnPhase.SelectHeadquarters"/>
/// (<see cref="GameMode.Capital"/> only) and never cleared — it designates a
/// territory, not who currently controls it (design D4: capture of the
/// territory is derivable from <c>Territories[HeadquartersId].Owner</c>, so
/// it is deliberately not stored here).
/// </summary>
public sealed record PlayerState(PlayerId Id, IReadOnlyList<Card> Hand, bool IsEliminated, int TroopsRemaining, bool IsNeutral = false, TerritoryId? HeadquartersId = null);
