using Risk.Domain.Cards;
using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>Raised when a player draws a card at the end of a turn in which they conquered at least one territory.</summary>
public sealed record CardDrawn(PlayerId Actor, Card Card) : GameEvent;
