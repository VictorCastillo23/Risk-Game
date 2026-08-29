using System.Text.RegularExpressions;
using Risk.Domain.Map;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class ContinentPaletteTests
{
    [Fact]
    public void Colors_ContainsEveryContinent_ExactlyOnce()
    {
        Assert.Equal(Continents.All.Count, ContinentPalette.Colors.Count);
        Assert.All(Continents.All, continent => Assert.True(ContinentPalette.Colors.ContainsKey(continent.Id)));
    }

    [Fact]
    public void Colors_AreAllValidHexColors()
    {
        Assert.All(ContinentPalette.Colors.Values, hex => Assert.Matches(new Regex("^#[0-9A-Fa-f]{6}$"), hex));
    }

    [Fact]
    public void Colors_AreAllMutuallyDistinct()
    {
        Assert.Equal(ContinentPalette.Colors.Count, ContinentPalette.Colors.Values.Distinct().Count());
    }

    [Fact]
    public void Colors_AreAllDistinctFromEveryPlayerSwatch()
    {
        Assert.All(ContinentPalette.Colors.Values, hex => Assert.DoesNotContain(hex, PlayerPalette.Swatches));
    }

    [Fact]
    public void ColorOf_ForARegisteredContinent_ReturnsItsConfiguredColor()
    {
        var continent = Continents.All[0];

        Assert.Equal(ContinentPalette.Colors[continent.Id], ContinentPalette.ColorOf(continent.Id));
    }

    [Fact]
    public void ColorOf_ForAnUnregisteredContinent_ReturnsTheNeutralFallback()
    {
        Assert.Equal("#9CA3AF", ContinentPalette.ColorOf(new ContinentId("XX")));
    }
}
