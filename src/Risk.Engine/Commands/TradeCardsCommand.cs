using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Commands;

/// <summary>
/// Trades a valid set of cards in for reinforcement troops. When two or
/// more of the traded cards name territories the actor currently owns,
/// <paramref name="BonusTerritory"/> selects which one receives the flat
/// +2-troop occupied-territory bonus (see <see cref="Risk.Engine.Rules.TerritoryTradeBonus"/>);
/// it is optional when there are zero or one matches.
/// </summary>
public sealed record TradeCardsCommand(PlayerId Actor, IReadOnlyList<Card> Cards, TerritoryId? BonusTerritory = null) : GameCommand(Actor);
