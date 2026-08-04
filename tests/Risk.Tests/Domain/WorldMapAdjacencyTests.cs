using Risk.Domain.Map;

namespace Risk.Tests.Domain;

public class WorldMapAdjacencyTests
{
    [Theory]
    [InlineData("Alaska", "Kamchatka")]
    [InlineData("Greenland", "Iceland")]
    [InlineData("Brazil", "NorthAfrica")]
    [InlineData("Alaska", "NorthwestTerritory")]
    [InlineData("Ukraine", "Ural")]
    [InlineData("Siam", "Indonesia")]
    public void AreAdjacent_returns_true_for_known_classic_connections(string a, string b)
    {
        Assert.True(WorldMap.AreAdjacent(new TerritoryId(a), new TerritoryId(b)));
    }

    [Theory]
    [InlineData("Alaska", "Brazil")]
    [InlineData("Madagascar", "Iceland")]
    [InlineData("Japan", "Peru")]
    public void AreAdjacent_returns_false_for_known_non_adjacent_territories(string a, string b)
    {
        Assert.False(WorldMap.AreAdjacent(new TerritoryId(a), new TerritoryId(b)));
    }

    [Fact]
    public void Adjacency_is_symmetric_for_every_territory_pair()
    {
        foreach (var territory in WorldMap.Territories)
        {
            foreach (var neighbor in WorldMap.NeighborsOf(territory.Id))
            {
                Assert.True(
                    WorldMap.AreAdjacent(neighbor, territory.Id),
                    $"{neighbor.Value} does not list {territory.Id.Value} back as a neighbor");
            }
        }
    }

    [Fact]
    public void No_territory_is_adjacency_isolated()
    {
        foreach (var territory in WorldMap.Territories)
        {
            Assert.NotEmpty(WorldMap.NeighborsOf(territory.Id));
        }
    }
}
