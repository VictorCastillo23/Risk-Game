using Risk.AI;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Views;

namespace Risk.Web.Tests.Fakes;

/// <summary>
/// Test-only <see cref="IBotPlayer"/> that always issues a phase-illegal
/// command (<see cref="EndPhaseCommand"/> during Claim), so session tests
/// can prove the hard-fail path: the engine rejects, the session surfaces
/// <c>AiFailure</c>, state stays put, and nothing retries.
/// </summary>
internal sealed class RejectingBotStub(PlayerId id) : IBotPlayer
{
    public PlayerId Id { get; } = id;

    public (GameCommand Command, BotMemory Memory) DecideNextCommand(PlayerView view, BotMemory memory) =>
        (new EndPhaseCommand(Id), memory);
}
