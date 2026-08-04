using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Rules;
using Risk.Engine.State;

namespace Risk.Tests.Engine;

public class ReinforcementTests
{
    [Fact]
    public void Calculate_owning_7_territories_with_no_continent_yields_the_minimum_of_3()
    {
        var player = new PlayerId(0);
        var other = new PlayerId(1);
        var territories = OwnedCountFor(player, other, ownedCount: 7);

        Assert.Equal(3, Reinforcement.Calculate(territories, player));
    }

    [Fact]
    public void Calculate_owning_15_territories_with_no_continent_yields_floor_division()
    {
        var player = new PlayerId(0);
        var other = new PlayerId(1);
        var territories = OwnedCountFor(player, other, ownedCount: 15);

        Assert.Equal(5, Reinforcement.Calculate(territories, player));
    }

    [Fact]
    public void Calculate_adds_the_continent_bonus_when_a_player_fully_controls_it()
    {
        var player = new PlayerId(0);
        var other = new PlayerId(1);
        var oceania = new ContinentId("OC");
        var territories = new Dictionary<TerritoryId, TerritoryState>();

        foreach (var territory in WorldMap.Territories)
        {
            var owner = territory.ContinentId == oceania ? player : other;
            territories[territory.Id] = new TerritoryState(owner, 1);
        }

        var troops = Reinforcement.Calculate(territories, player);

        // 4 Oceania territories owned -> floor(4/3)=1 -> minimum of 3 territory
        // troops, plus Oceania's +2 continent bonus for full control = 5.
        Assert.Equal(5, troops);
    }

    [Fact]
    public void Calculate_does_not_award_a_continent_bonus_for_partial_control()
    {
        var player = new PlayerId(0);
        var other = new PlayerId(1);
        var oceania = new ContinentId("OC");
        var oceaniaMembers = Continents.All.Single(c => c.Id == oceania).Members;
        var territories = new Dictionary<TerritoryId, TerritoryState>();

        foreach (var territory in WorldMap.Territories)
        {
            var ownsThisOne = oceaniaMembers.Contains(territory.Id) && territory.Id != oceaniaMembers[^1];
            territories[territory.Id] = new TerritoryState(ownsThisOne ? player : other, 1);
        }

        var troops = Reinforcement.Calculate(territories, player);

        // Owns 3 of Oceania's 4 territories -> no bonus; floor(3/3)=1 -> minimum of 3.
        Assert.Equal(3, troops);
    }

    /// <summary>
    /// Builds a board where <paramref name="player"/> owns exactly
    /// <paramref name="ownedCount"/> territories, deliberately never
    /// completing any continent (one member per continent is always left to
    /// <paramref name="other"/>), so this fixture stays a pure "no continent
    /// bonus" scenario regardless of which territories land in the count.
    /// </summary>
    private static Dictionary<TerritoryId, TerritoryState> OwnedCountFor(PlayerId player, PlayerId other, int ownedCount)
    {
        var oneMemberPerContinent = WorldMap.Territories
            .GroupBy(t => t.ContinentId)
            .Select(g => g.Last().Id)
            .ToHashSet();

        var eligibleForPlayer = WorldMap.Territories
            .Where(t => !oneMemberPerContinent.Contains(t.Id))
            .Select(t => t.Id)
            .ToList();

        var owned = eligibleForPlayer.Take(ownedCount).ToHashSet();
        var territories = new Dictionary<TerritoryId, TerritoryState>();

        foreach (var territory in WorldMap.Territories)
        {
            territories[territory.Id] = new TerritoryState(owned.Contains(territory.Id) ? player : other, 1);
        }

        return territories;
    }
}
