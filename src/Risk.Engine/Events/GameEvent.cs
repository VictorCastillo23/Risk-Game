namespace Risk.Engine.Events;

/// <summary>
/// Something that happened while executing a command. Returned as a delta
/// from <c>GameEngine.Execute</c> and appended to <c>GameState.Log</c>.
/// </summary>
public abstract record GameEvent;
