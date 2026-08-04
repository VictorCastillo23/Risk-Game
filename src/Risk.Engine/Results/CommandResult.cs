using Risk.Domain.Errors;

namespace Risk.Engine.Results;

/// <summary>
/// Result of executing a command against the engine: either the command
/// succeeded and produced a new state plus the events that occurred, or it
/// was rejected and the state is left unchanged.
/// </summary>
/// <typeparam name="TState">The state type produced on success.</typeparam>
/// <typeparam name="TEvent">The event type describing what happened.</typeparam>
public abstract record CommandResult<TState, TEvent>
{
    private CommandResult()
    {
    }

    /// <summary>The command succeeded; carries the resulting state and the events it produced.</summary>
    public sealed record Ok(TState State, IReadOnlyList<TEvent> Events) : CommandResult<TState, TEvent>;

    /// <summary>The command was rejected; the state is unchanged.</summary>
    public sealed record Rejected(GameError Error) : CommandResult<TState, TEvent>;
}
