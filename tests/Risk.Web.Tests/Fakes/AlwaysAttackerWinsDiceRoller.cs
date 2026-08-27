using Risk.Domain.Dice;

namespace Risk.Web.Tests.Fakes;

/// <summary>
/// A deterministic <see cref="IDiceRoller"/> for the full-game integration
/// test: alternates between an all-sixes roll (attacker's call) and an
/// all-ones roll (defender's call), since <c>GameEngine.ExecuteAttack</c>
/// always calls <see cref="Roll"/> exactly twice per <c>AttackCommand</c>,
/// attacker first. This guarantees the attacker wins every pairwise
/// comparison without relying on real randomness.
///
/// Mirrors <c>Risk.Tests.Fakes.AlwaysAttackerWinsDiceRoller</c> exactly.
/// It cannot be shared directly (that type is <c>internal</c> to
/// <c>Risk.Tests</c>, a separate assembly with no
/// <c>InternalsVisibleTo</c>), so this is a deliberate, minimal mirror
/// rather than a new fake invented from scratch — same shape, same
/// codebase convention (<c>Risk.Web.Tests/Fakes/</c> mirrors
/// <c>Risk.Tests/Fakes/</c>).
/// </summary>
internal sealed class AlwaysAttackerWinsDiceRoller : IDiceRoller
{
    private bool _nextRollIsAttacker = true;

    public IReadOnlyList<int> Roll(int count)
    {
        var value = _nextRollIsAttacker ? 6 : 1;
        _nextRollIsAttacker = !_nextRollIsAttacker;
        return Enumerable.Repeat(value, count).ToArray();
    }
}
