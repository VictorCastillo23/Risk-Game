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
    public void Coordinates_AllFallWithinTheDeclaredCanvasBounds()
    {
        Assert.All(TerritoryLayout.Coordinates.Values, position =>
        {
            Assert.InRange(position.X, 0, TerritoryLayout.CanvasWidth);
            Assert.InRange(position.Y, 0, TerritoryLayout.CanvasHeight);
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
}
