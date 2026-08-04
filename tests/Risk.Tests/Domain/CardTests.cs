using Risk.Domain.Cards;
using Risk.Domain.Map;

namespace Risk.Tests.Domain;

public class CardTests
{
    [Fact]
    public void TerritoryCard_holds_territory_and_symbol()
    {
        Card card = new TerritoryCard(new TerritoryId("Egypt"), CardSymbol.Artillery);

        var territoryCard = Assert.IsType<TerritoryCard>(card);
        Assert.Equal(new TerritoryId("Egypt"), territoryCard.Territory);
        Assert.Equal(CardSymbol.Artillery, territoryCard.Symbol);
    }

    [Fact]
    public void WildCard_is_distinct_from_a_territory_card()
    {
        Card wild = new WildCard();
        Card territoryCard = new TerritoryCard(new TerritoryId("Egypt"), CardSymbol.Artillery);

        Assert.IsType<WildCard>(wild);
        Assert.IsType<TerritoryCard>(territoryCard);
        Assert.IsNotType<WildCard>(territoryCard);
    }
}
