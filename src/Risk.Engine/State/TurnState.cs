using Risk.Domain.Players;

namespace Risk.Engine.State;

/// <summary>Whose turn it is and which phase of that turn is active.</summary>
public sealed record TurnState(PlayerId CurrentPlayer, TurnPhase Phase);
