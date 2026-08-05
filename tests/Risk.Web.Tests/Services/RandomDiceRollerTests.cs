using Risk.Web.Services;

namespace Risk.Web.Tests.Services;

public class RandomDiceRollerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Roll_ReturnsRequestedNumberOfDice(int count)
    {
        var roller = new RandomDiceRoller();

        var rolls = roller.Roll(count);

        Assert.Equal(count, rolls.Count);
    }

    [Fact]
    public void Roll_EveryValueIsWithinDieRange_OverManySamples()
    {
        var roller = new RandomDiceRoller();

        for (var i = 0; i < 500; i++)
        {
            var rolls = roller.Roll(3);

            Assert.All(rolls, value => Assert.InRange(value, 1, 6));
        }
    }
}
