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

    [Fact]
    public void Polygons_ContainsEveryWorldMapTerritory_ExactlyOnce()
    {
        var expectedIds = WorldMap.Territories.Select(t => t.Id).ToHashSet();

        Assert.Equal(expectedIds.Count, TerritoryLayout.Polygons.Count);
        Assert.All(expectedIds, id => Assert.True(TerritoryLayout.Polygons.ContainsKey(id)));
    }

    [Fact]
    public void Polygons_EveryTerritoryHasASixPointHexagon_WithNoDegenerateOrOutOfBoundsVertices()
    {
        Assert.All(TerritoryLayout.Polygons.Values, polygon =>
        {
            Assert.Equal(6, polygon.Count);
            Assert.Equal(6, polygon.Distinct().Count());

            Assert.All(polygon, point =>
            {
                Assert.False(double.IsNaN(point.X) || double.IsInfinity(point.X));
                Assert.False(double.IsNaN(point.Y) || double.IsInfinity(point.Y));
                Assert.InRange(point.X, 0, TerritoryLayout.CanvasWidth);
                Assert.InRange(point.Y, 0, TerritoryLayout.CanvasHeight);
            });
        });
    }

    [Fact]
    public void PolygonPointsAttr_ContainsEveryTerritory_AsANonEmptyString()
    {
        var expectedIds = WorldMap.Territories.Select(t => t.Id).ToHashSet();

        Assert.Equal(expectedIds.Count, TerritoryLayout.PolygonPointsAttr.Count);
        Assert.All(TerritoryLayout.PolygonPointsAttr.Values, attr => Assert.False(string.IsNullOrWhiteSpace(attr)));
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

    [Fact]
    public void ContinentBounds_ContainsAllSixContinents_WithPositiveExtents()
    {
        Assert.Equal(Continents.All.Count, TerritoryLayout.ContinentBounds.Count);

        foreach (var continent in Continents.All)
        {
            Assert.True(TerritoryLayout.ContinentBounds.ContainsKey(continent.Id));

            var bounds = TerritoryLayout.ContinentBounds[continent.Id];
            Assert.True(bounds.Width > 0);
            Assert.True(bounds.Height > 0);
        }
    }

    [Fact]
    public void ContinentBounds_NoTwoContinentHalosOverlap()
    {
        var bounds = TerritoryLayout.ContinentBounds.ToArray();

        for (var i = 0; i < bounds.Length; i++)
        {
            for (var j = i + 1; j < bounds.Length; j++)
            {
                var (idA, a) = bounds[i];
                var (idB, b) = bounds[j];

                var overlaps = a.X < b.X + b.Width && b.X < a.X + a.Width &&
                               a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

                Assert.False(overlaps, $"{idA} and {idB} continent halos overlap.");
            }
        }
    }
}
