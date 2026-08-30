using Risk.Domain.Players;
using Risk.Engine.Modes;
using Risk.Engine.State;

namespace Risk.Tests.Fakes;

/// <summary>
/// An <see cref="IVictoryRule"/> decorator that wraps a real rule and counts
/// how many times <see cref="CheckVictory"/> was invoked, so tests can
/// positively prove a given <see cref="GameMode"/> is actually routed
/// through the wrapped rule instead of inferring it from assertions that
/// would also pass via a byte-identical inline fallback.
/// </summary>
internal sealed class RecordingVictoryRule(IVictoryRule inner) : IVictoryRule
{
    public int Calls { get; private set; }

    public PlayerId? CheckVictory(GameState state)
    {
        Calls++;
        return inner.CheckVictory(state);
    }
}
