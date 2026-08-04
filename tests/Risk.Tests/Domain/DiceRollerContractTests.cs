using Risk.Domain.Dice;

namespace Risk.Tests.Domain;

/// <summary>
/// A stand-in implementation used only to prove the <see cref="IDiceRoller"/>
/// contract is honoured. The real deterministic fake (QueuedDiceRoller) is
/// introduced later, alongside combat resolution.
/// </summary>
file sealed class FixedSequenceDiceRoller(IReadOnlyList<int> values) : IDiceRoller
{
    public IReadOnlyList<int> Roll(int count) => values.Take(count).ToArray();
}

public class DiceRollerContractTests
{
    [Fact]
    public void Roll_returns_requested_number_of_values()
    {
        IDiceRoller roller = new FixedSequenceDiceRoller([6, 4, 1]);

        var result = roller.Roll(2);

        Assert.Equal(new[] { 6, 4 }, result);
    }

    [Fact]
    public void Roll_with_different_count_returns_matching_number_of_values()
    {
        IDiceRoller roller = new FixedSequenceDiceRoller([3, 5]);

        var result = roller.Roll(1);

        Assert.Equal(new[] { 3 }, result);
    }
}
