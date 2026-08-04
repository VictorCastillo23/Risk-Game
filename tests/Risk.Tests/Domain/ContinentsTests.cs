using Risk.Domain.Map;

namespace Risk.Tests.Domain;

public class ContinentsTests
{
    [Fact]
    public void All_returns_six_continents()
    {
        Assert.Equal(6, Continents.All.Count);
    }

    [Theory]
    [InlineData("NA", 5)]
    [InlineData("SA", 2)]
    [InlineData("EU", 5)]
    [InlineData("AF", 3)]
    [InlineData("AS", 7)]
    [InlineData("OC", 2)]
    public void Continent_bonus_matches_classic_table(string continentId, int expectedBonus)
    {
        var continent = Continents.All.Single(c => c.Id == new ContinentId(continentId));

        Assert.Equal(expectedBonus, continent.Bonus);
    }
}
