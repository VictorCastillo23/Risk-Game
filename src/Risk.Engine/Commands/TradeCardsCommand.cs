using Risk.Domain.Cards;
using Risk.Domain.Players;

namespace Risk.Engine.Commands;

/// <summary>Trades a valid set of cards in for reinforcement troops.</summary>
public sealed record TradeCardsCommand(PlayerId Actor, IReadOnlyList<Card> Cards) : GameCommand(Actor);
