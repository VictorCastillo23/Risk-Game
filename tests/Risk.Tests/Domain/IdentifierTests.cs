using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.Tests.Domain;

public class IdentifierTests
{
    [Fact]
    public void PlayerId_has_value_equality()
    {
        var a = new PlayerId(1);
        var b = new PlayerId(1);
        var c = new PlayerId(2);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void TerritoryId_has_value_equality()
    {
        var a = new TerritoryId("Alaska");
        var b = new TerritoryId("Alaska");
        var c = new TerritoryId("Peru");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal("Alaska", a.Value);
    }

    [Fact]
    public void ContinentId_has_value_equality()
    {
        var a = new ContinentId("NA");
        var b = new ContinentId("NA");
        var c = new ContinentId("SA");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal("NA", a.Value);
    }
}
