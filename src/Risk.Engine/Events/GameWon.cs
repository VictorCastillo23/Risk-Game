using Risk.Domain.Players;

namespace Risk.Engine.Events;

/// <summary>Raised when a player captures the last territory not already theirs, controlling all 42 territories.</summary>
public sealed record GameWon(PlayerId Winner) : GameEvent;
