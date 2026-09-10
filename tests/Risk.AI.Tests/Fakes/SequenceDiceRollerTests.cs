namespace Risk.AI.Tests.Fakes;

public class SequenceDiceRollerTests
{
    [Fact]
    public void Roll_returns_values_from_the_provided_sequence_in_order()
    {
        var roller = new SequenceDiceRoller([1, 2, 3, 4, 5]);

        Assert.Equal(new[] { 1 }, roller.Roll(1));
        Assert.Equal(new[] { 2, 3 }, roller.Roll(2));
    }

    [Fact]
    public void Roll_wraps_around_when_the_sequence_is_exhausted()
    {
        var roller = new SequenceDiceRoller([1, 2, 3]);

        roller.Roll(2); // consumes 1, 2
        var result = roller.Roll(3); // consumes 3, then wraps to 1, 2

        Assert.Equal(new[] { 3, 1, 2 }, result);
    }

    [Fact]
    public void Roll_never_throws_across_many_more_calls_than_the_sequence_length()
    {
        var roller = new SequenceDiceRoller([6, 5, 4]);

        for (var i = 0; i < 1000; i++)
        {
            var result = roller.Roll(3);
            Assert.Equal(3, result.Count);
        }
    }

    [Fact]
    public void Default_sequence_length_is_prime()
    {
        var roller = new SequenceDiceRoller();

        Assert.True(IsPrime(roller.SequenceLength));
    }

    [Fact]
    public void Default_sequence_only_contains_valid_die_values()
    {
        var roller = new SequenceDiceRoller();

        for (var i = 0; i < roller.SequenceLength * 2; i++)
        {
            var value = roller.Roll(1)[0];
            Assert.InRange(value, 1, 6);
        }
    }

    [Fact]
    public void Constructor_rejects_an_empty_sequence()
    {
        Assert.Throws<ArgumentException>(() => new SequenceDiceRoller([]));
    }

    private static bool IsPrime(int n)
    {
        if (n < 2)
        {
            return false;
        }

        for (var i = 2; i * i <= n; i++)
        {
            if (n % i == 0)
            {
                return false;
            }
        }

        return true;
    }
}
