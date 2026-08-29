using Risk.Domain.Map;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class BoardEdgesTests
{
    [Fact]
    public void UniqueEdges_ContainsEachAdjacentPairExactlyOnce_NotBothDirections()
    {
        var edges = BoardEdges.UniqueEdges();

        var alaska = new TerritoryId("Alaska");
        var alberta = new TerritoryId("Alberta");

        var matches = edges.Count(edge =>
            (edge.A == alaska && edge.B == alberta) ||
            (edge.A == alberta && edge.B == alaska));

        Assert.Equal(1, matches);
    }

    [Fact]
    public void UniqueEdges_CountMatchesHalfOfTotalDirectedAdjacencyEntries()
    {
        var edges = BoardEdges.UniqueEdges();

        var directedCount = WorldMap.Territories
            .Sum(territory => WorldMap.NeighborsOf(territory.Id).Count);

        Assert.Equal(directedCount / 2, edges.Count);
    }

    [Fact]
    public void IsSeaRoute_ForAllFiveClassicSeaRoutes_ReturnsTrueRegardlessOfArgumentOrder()
    {
        (string A, string B)[] seaRoutes =
        [
            ("Alaska", "Kamchatka"),
            ("Greenland", "Iceland"),
            ("Brazil", "NorthAfrica"),
            ("WesternEurope", "NorthAfrica"),
            ("Siam", "Indonesia")
        ];

        foreach (var (a, b) in seaRoutes)
        {
            Assert.True(BoardEdges.IsSeaRoute(new TerritoryId(a), new TerritoryId(b)));
            Assert.True(BoardEdges.IsSeaRoute(new TerritoryId(b), new TerritoryId(a)));
        }
    }

    [Fact]
    public void IsSeaRoute_ForALandBorder_ReturnsFalse()
    {
        Assert.False(BoardEdges.IsSeaRoute(new TerritoryId("Alaska"), new TerritoryId("Alberta")));
    }
}
