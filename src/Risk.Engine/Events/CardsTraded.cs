using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>
/// Raised when a player trades in a valid card set for reinforcement
/// troops. <see cref="Bonus"/> is the escalating pool bonus only.
/// <see cref="BonusTerritory"/> is set only when the flat +2
/// occupied-territory bonus was applied, and identifies where it landed.
/// </summary>
public sealed record CardsTraded(PlayerId Actor, IReadOnlyList<Card> Cards, int Bonus, TerritoryId? BonusTerritory = null) : GameEvent;
