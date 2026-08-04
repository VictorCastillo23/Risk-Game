using Risk.Domain.Cards;
using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>Raised when a player trades in a valid card set for reinforcement troops.</summary>
public sealed record CardsTraded(PlayerId Actor, IReadOnlyList<Card> Cards, int Bonus) : GameEvent;
