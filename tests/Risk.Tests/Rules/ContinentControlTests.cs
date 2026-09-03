using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Rules;
using Risk.Engine.State;

namespace Risk.Tests.Rules;

public class ContinentControlTests
{
    private static readonly PlayerId PlayerA = new(0);
    private static readonly PlayerId PlayerB = new(1);

    private static readonly TerritoryId TerritoryOne = new("t1");
    private static readonly TerritoryId TerritoryTwo = new("t2");

    private static readonly Continent TwoMemberContinent = new(
        new ContinentId("XX"),
        "Test Continent",
        Bonus: 4,
        Members: [TerritoryOne, TerritoryTwo]);

    [Fact]
    public void IsFullyOwnedBy_returns_true_when_every_member_is_owned_by_the_player()
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>
        {
            [TerritoryOne] = new(PlayerA, 1),
            [TerritoryTwo] = new(PlayerA, 3)
        };

        Assert.True(ContinentControl.IsFullyOwnedBy(TwoMemberContinent, territories, PlayerA));
    }

    [Fact]
    public void IsFullyOwnedBy_returns_false_when_one_member_is_owned_by_someone_else()
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>
        {
            [TerritoryOne] = new(PlayerA, 1),
            [TerritoryTwo] = new(PlayerB, 3)
        };

        Assert.False(ContinentControl.IsFullyOwnedBy(TwoMemberContinent, territories, PlayerA));
    }

    [Fact]
    public void IsFullyOwnedBy_returns_false_when_a_member_territory_is_absent_from_the_dictionary()
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>
        {
            [TerritoryOne] = new(PlayerA, 1)
            // TerritoryTwo intentionally missing
        };

        Assert.False(ContinentControl.IsFullyOwnedBy(TwoMemberContinent, territories, PlayerA));
    }

    [Fact]
    public void IsFullyOwnedBy_returns_false_when_the_player_owns_no_members()
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>
        {
            [TerritoryOne] = new(PlayerB, 1),
            [TerritoryTwo] = new(PlayerB, 3)
        };

        Assert.False(ContinentControl.IsFullyOwnedBy(TwoMemberContinent, territories, PlayerA));
    }
}
