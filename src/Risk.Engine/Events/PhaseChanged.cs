using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Engine.Events;

/// <summary>Raised when the turn phase transitions (e.g. Setup completing into the first Reinforce phase).</summary>
public sealed record PhaseChanged(TurnPhase From, TurnPhase To, PlayerId CurrentPlayer) : GameEvent;
