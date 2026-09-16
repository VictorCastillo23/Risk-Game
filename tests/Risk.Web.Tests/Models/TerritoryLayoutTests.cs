using Risk.Domain.Map;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class TerritoryLayoutTests
{
    [Fact]
    public void Coordinates_ContainsEveryWorldMapTerritory_ExactlyOnce()
    {
        var expectedIds = WorldMap.Territories.Select(t => t.Id).ToHashSet();

        Assert.Equal(expectedIds.Count, TerritoryLayout.Coordinates.Count);
        Assert.All(expectedIds, id => Assert.True(TerritoryLayout.Coordinates.ContainsKey(id)));
    }

    [Fact]
    public void Coordinates_HasNoExtraEntriesBeyondWorldMapTerritories()
    {
        var expectedIds = WorldMap.Territories.Select(t => t.Id).ToHashSet();

        Assert.All(TerritoryLayout.Coordinates.Keys, id => Assert.Contains(id, expectedIds));
    }

    [Fact]
    public void Coordinates_HasNoDuplicatePositions()
    {
        var distinctPositions = TerritoryLayout.Coordinates.Values.Distinct().Count();

        Assert.Equal(TerritoryLayout.Coordinates.Count, distinctPositions);
    }

    [Fact]
    public void Coordinates_HasNoNaNOrInfiniteValues()
    {
        Assert.All(TerritoryLayout.Coordinates.Values, position =>
        {
            Assert.False(double.IsNaN(position.X) || double.IsInfinity(position.X));
            Assert.False(double.IsNaN(position.Y) || double.IsInfinity(position.Y));
        });
    }

    [Fact]
    public void Coordinates_EveryAdjacencyPairHasBothEndpointsLaidOut()
    {
        foreach (var territory in WorldMap.Territories)
        {
            foreach (var neighbor in WorldMap.NeighborsOf(territory.Id))
            {
                Assert.True(TerritoryLayout.Coordinates.ContainsKey(territory.Id));
                Assert.True(TerritoryLayout.Coordinates.ContainsKey(neighbor));
            }
        }
    }

    [Fact]
    public void ContinentOf_ContainsEveryWorldMapTerritory_MatchingItsRealContinent()
    {
        foreach (var territory in WorldMap.Territories)
        {
            Assert.True(TerritoryLayout.ContinentOf.ContainsKey(territory.Id));
            Assert.Equal(territory.ContinentId, TerritoryLayout.ContinentOf[territory.Id]);
        }
    }

    // Remediation for sdd-verify's CRITICAL-1: a one-time pixel measurement
    // against the real map artwork found exactly these 5 territories' local
    // art dark enough that the default outer ring ink (MarkerInk.Outer) fails
    // WCAG 1.4.11's 3:1 floor. This is a named per-territory exception list,
    // the same shape as design D9's RadiusOverrides, but for ring color.
    [Theory]
    [InlineData("Kamchatka", true)]
    [InlineData("Japan", true)]
    [InlineData("Indonesia", true)]
    [InlineData("NewGuinea", true)]
    [InlineData("Madagascar", true)]
    [InlineData("Alaska", false)]
    [InlineData("Brazil", false)]
    [InlineData("Ukraine", false)]
    public void NeedsLightOuterRing_IsTrueOnlyForTheFiveDarkArtTerritories(string territoryName, bool expected)
    {
        var needsLightRing = TerritoryLayout.NeedsLightOuterRing(new TerritoryId(territoryName));

        Assert.Equal(expected, needsLightRing);
    }
}
