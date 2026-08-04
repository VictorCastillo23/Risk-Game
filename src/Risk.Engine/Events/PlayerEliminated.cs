using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>Raised when a player loses their last territory: their whole hand transfers to the eliminator.</summary>
public sealed record PlayerEliminated(PlayerId Victim, PlayerId By, int CardsTransferred) : GameEvent;
