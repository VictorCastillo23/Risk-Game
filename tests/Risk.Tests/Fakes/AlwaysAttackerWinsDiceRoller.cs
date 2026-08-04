using Risk.Domain.Dice;

namespace Risk.Tests.Fakes;

/// <summary>
/// A deterministic <see cref="IDiceRoller"/> for scripted full-game
/// integration tests: alternates between an all-sixes roll (for the
/// attacker's call) and an all-ones roll (for the defender's call), since
/// <c>GameEngine.ExecuteAttack</c> always calls <see cref="Roll"/> exactly
/// twice per <c>AttackCommand</c>, attacker first. This guarantees the
/// attacker wins every pairwise comparison without relying on real
/// randomness, unlike <see cref="QueuedDiceRoller"/> which needs the exact
/// number of battle rounds known in advance (impractical for a scripted
/// game whose territory deal comes from real <c>GameSetup.Create</c>
/// randomness).
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
