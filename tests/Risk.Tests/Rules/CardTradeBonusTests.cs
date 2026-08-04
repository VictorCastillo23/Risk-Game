using Risk.Engine.Rules;

namespace Risk.Tests.Rules;

public class CardTradeBonusTests
{
    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 6)]
    [InlineData(3, 8)]
    [InlineData(4, 10)]
    [InlineData(5, 12)]
    [InlineData(6, 15)]
    [InlineData(7, 20)] // spec scenario: 6 prior trades -> 7th trade bonus is 20
    [InlineData(8, 25)]
    public void ForTradeNumber_matches_the_classic_escalating_scale(int tradeNumber, int expectedBonus)
    {
        Assert.Equal(expectedBonus, CardTradeBonus.ForTradeNumber(tradeNumber));
    }
}
