using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>
/// Raised exactly once, at the moment the last player selects their
/// headquarters and the phase transitions out of
/// <see cref="Risk.Engine.State.TurnPhase.SelectHeadquarters"/>. Carries
/// every player's headquarters territory, now that secrecy no longer
/// applies.
/// </summary>
public sealed record HeadquartersRevealed(IReadOnlyDictionary<PlayerId, TerritoryId> Headquarters) : GameEvent;
