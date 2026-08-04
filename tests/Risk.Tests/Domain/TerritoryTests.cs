using Risk.Domain.Cards;
using Risk.Domain.Map;

namespace Risk.Tests.Domain;

public class TerritoryTests
{
    [Fact]
    public void Territory_holds_id_continent_and_card_symbol()
    {
        var territory = new Territory(new TerritoryId("Alaska"), new ContinentId("NA"), CardSymbol.Infantry);

        Assert.Equal(new TerritoryId("Alaska"), territory.Id);
        Assert.Equal(new ContinentId("NA"), territory.ContinentId);
        Assert.Equal(CardSymbol.Infantry, territory.Symbol);
    }

    [Fact]
    public void Territory_has_value_equality()
    {
        var a = new Territory(new TerritoryId("Peru"), new ContinentId("SA"), CardSymbol.Cavalry);
        var b = new Territory(new TerritoryId("Peru"), new ContinentId("SA"), CardSymbol.Cavalry);
        var different = new Territory(new TerritoryId("Brazil"), new ContinentId("SA"), CardSymbol.Cavalry);

        Assert.Equal(a, b);
        Assert.NotEqual(a, different);
    }
}
