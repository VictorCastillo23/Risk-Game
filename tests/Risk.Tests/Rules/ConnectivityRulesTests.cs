using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Rules;
using Risk.Engine.State;

namespace Risk.Tests.Rules;

public class ConnectivityRulesTests
{
    // NA chain used to build connectivity scenarios:
    // Alaska -- NorthwestTerritory -- Alberta -- Ontario -- Quebec
    // Alaska is NOT directly adjacent to Ontario or Quebec.
    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId NorthwestTerritory = new("NorthwestTerritory");
    private static readonly TerritoryId Alberta = new("Alberta");
    private static readonly TerritoryId Ontario = new("Ontario");
    private static readonly TerritoryId Quebec = new("Quebec");

    // The only edge connecting North America and South America in the world
    // map is CentralAmerica <-> Venezuela, making CentralAmerica a natural
    // single-point-of-failure chokepoint for a "path blocked by an enemy
    // territory" scenario.
    private static readonly TerritoryId WesternUnitedStates = new("WesternUnitedStates");
    private static readonly TerritoryId CentralAmerica = new("CentralAmerica");
    private static readonly TerritoryId Venezuela = new("Venezuela");

    private static readonly PlayerId Owner = new(0);
    private static readonly PlayerId Other = new(1);

    [Fact]
    public void HasFriendlyPath_ReturnsTrue_ForDirectAdjacency()
    {
        var territories = Build(owned: [Alaska, NorthwestTerritory]);

        Assert.True(ConnectivityRules.HasFriendlyPath(territories, Owner, Alaska, NorthwestTerritory));
    }

    [Fact]
    public void HasFriendlyPath_ReturnsTrue_ForMultiHopChainThroughOwnedTerritories()
    {
        var territories = Build(owned: [Alaska, NorthwestTerritory, Alberta, Ontario, Quebec]);

        Assert.True(ConnectivityRules.HasFriendlyPath(territories, Owner, Alaska, Quebec));
    }

    [Fact]
    public void HasFriendlyPath_ReturnsFalse_WhenPathBlockedByEnemyTerritory()
    {
        // WesternUnitedStates and Venezuela are both owned by Owner, but
        // every other territory (including the sole connecting territory,
        // CentralAmerica) belongs to Other, so no all-owned chain exists.
        var territories = Build(owned: [WesternUnitedStates, Venezuela]);

        Assert.False(ConnectivityRules.HasFriendlyPath(territories, Owner, WesternUnitedStates, Venezuela));
    }

    [Fact]
    public void HasFriendlyPath_ReturnsFalse_WhenNoPathExistsAtAll()
    {
        var territories = Build(owned: [Alaska]);

        Assert.False(ConnectivityRules.HasFriendlyPath(territories, Owner, Alaska, Quebec));
    }

    [Fact]
    public void HasFriendlyPath_ReturnsTrue_WhenFromEqualsTo()
    {
        var territories = Build(owned: [Alaska]);

        Assert.True(ConnectivityRules.HasFriendlyPath(territories, Owner, Alaska, Alaska));
    }

    private static Dictionary<TerritoryId, TerritoryState> Build(IReadOnlyList<TerritoryId> owned)
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>();

        foreach (var territory in WorldMap.Territories)
        {
            var owner = owned.Contains(territory.Id) ? Owner : Other;
            territories[territory.Id] = new TerritoryState(owner, 1);
        }

        return territories;
    }
}
