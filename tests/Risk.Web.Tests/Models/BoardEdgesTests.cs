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
}
