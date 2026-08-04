using Risk.Domain.Cards;
using Risk.Domain.Players;

namespace Risk.Engine.State;

/// <summary>
/// A player's game state: their hand, whether they're eliminated, and any
/// troops they still need to place (starting troops during Setup, or the
/// current turn's reinforcement during Reinforce).
/// </summary>
public sealed record PlayerState(PlayerId Id, IReadOnlyList<Card> Hand, bool IsEliminated, int TroopsRemaining);
