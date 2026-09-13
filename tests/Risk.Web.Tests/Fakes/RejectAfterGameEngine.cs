using Risk.Domain.Errors;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Engine.Views;

namespace Risk.Web.Tests.Fakes;

/// <summary>
/// Decorator over a real <see cref="IGameEngine"/> that forces every command
/// issued while it is <paramref name="rejectFor"/>'s turn to be rejected,
/// while every other seat's command passes straight through to
/// <paramref name="inner"/> unchanged. Used to prove
/// <c>GameSessionService.AdvanceAiTurns</c>'s hard-fail contract (design D2):
/// a bot's illegal command must surface as <c>AiTurnFailure</c>, never be
/// retried or masked.
///
/// Distinct from <see cref="FakeGameEngine"/> (a preloaded-result stub with
/// no inner engine at all) — this one wraps a real engine so every OTHER
/// seat still plays a fully legal game around the forced rejection.
/// </summary>
internal sealed class RejectAfterGameEngine(IGameEngine inner, PlayerId rejectFor) : IGameEngine
{
    /// <summary>
    /// How many commands this fake has forced to <c>Rejected</c>. Used to
    /// prove the no-retry invariant: after a forced rejection, this must
    /// stay exactly 1 — a retried/looping caller would drive it higher.
    /// </summary>
    public int Rejections { get; private set; }

    public CommandResult<GameState, GameEvent> Execute(GameState state, GameCommand command) =>
        state.Turn.CurrentPlayer == rejectFor
            ? Reject()
            : inner.Execute(state, command);

    public PlayerView Observe(GameState state, PlayerId viewer) => inner.Observe(state, viewer);

    private CommandResult<GameState, GameEvent> Reject()
    {
        Rejections++;
        return new CommandResult<GameState, GameEvent>.Rejected(
            new GameError(GameErrorCode.NotYourTurn, "Forced rejection (RejectAfterGameEngine test fake)."));
    }
}
