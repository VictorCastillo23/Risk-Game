using Risk.Domain.Map;

namespace Risk.Tests.Domain;

public class WorldMapTerritoriesTests
{
    [Fact]
    public void Territories_returns_exactly_42_territories()
    {
        Assert.Equal(42, WorldMap.Territories.Count);
    }

    [Fact]
    public void Territories_are_all_distinct()
    {
        var distinctCount = WorldMap.Territories.Select(t => t.Id).Distinct().Count();

        Assert.Equal(42, distinctCount);
    }

    [Theory]
    [InlineData("Alaska", "NA")]
    [InlineData("Venezuela", "SA")]
    [InlineData("Ukraine", "EU")]
    [InlineData("Madagascar", "AF")]
    [InlineData("MiddleEast", "AS")]
    [InlineData("EasternAustralia", "OC")]
    public void Territories_assign_known_territory_to_correct_continent(string territoryName, string continentId)
    {
        var territory = WorldMap.Territories.Single(t => t.Id == new TerritoryId(territoryName));

        Assert.Equal(new ContinentId(continentId), territory.ContinentId);
    }

    [Theory]
    [InlineData("NA", 9)]
    [InlineData("SA", 4)]
    [InlineData("EU", 7)]
    [InlineData("AF", 6)]
    [InlineData("AS", 12)]
    [InlineData("OC", 4)]
    public void Continents_All_members_match_classic_territory_counts(string continentId, int expectedCount)
    {
        var continent = Continents.All.Single(c => c.Id == new ContinentId(continentId));

        Assert.Equal(expectedCount, continent.Members.Count);
    }

    [Fact]
    public void Continents_All_members_are_the_real_territory_ids_from_WorldMap()
    {
        var oceania = Continents.All.Single(c => c.Id == new ContinentId("OC"));

        Assert.Contains(new TerritoryId("Indonesia"), oceania.Members);
        Assert.Contains(new TerritoryId("NewGuinea"), oceania.Members);
        Assert.DoesNotContain(new TerritoryId("Alaska"), oceania.Members);
    }
}
