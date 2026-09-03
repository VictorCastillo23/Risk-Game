using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Engine.Views;

namespace Risk.Web.Tests.Fakes;

/// <summary>
/// Test-only <see cref="IGameEngine"/> double that returns a pre-loaded
/// result/view regardless of the state passed in, so
/// <c>GameSessionService</c> tests can isolate its own mutation/event logic
/// from the real engine's rules.
/// </summary>
internal sealed class FakeGameEngine : IGameEngine
{
    public CommandResult<GameState, GameEvent>? ExecuteResult { get; set; }
    public PlayerView? ObserveResult { get; set; }
    public GameCommand? LastCommand { get; private set; }

    /// <summary>
    /// Records the last <paramref name="viewer"/> passed to <see cref="Observe"/>,
    /// so tests of self-gating accessors (e.g.
    /// <c>GameSessionService.WinnerMission</c>) can assert both that
    /// <see cref="Observe"/> was called with the correct player AND, by
    /// staying <see langword="null"/>, that it was never called at all
    /// (design 3.4-D4's "structurally incapable of leaking mid-game").
    /// </summary>
    public PlayerId? LastObserveViewer { get; private set; }

    public CommandResult<GameState, GameEvent> Execute(GameState state, GameCommand command)
    {
        LastCommand = command;
        return ExecuteResult ?? throw new InvalidOperationException("FakeGameEngine.ExecuteResult was not set.");
    }

    public PlayerView Observe(GameState state, PlayerId viewer)
    {
        LastObserveViewer = viewer;
        return ObserveResult ?? throw new InvalidOperationException("FakeGameEngine.ObserveResult was not set.");
    }
}
