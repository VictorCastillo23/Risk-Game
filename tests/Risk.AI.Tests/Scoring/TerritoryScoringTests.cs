using Risk.AI.Scoring;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.AI.Tests.Scoring;

public class TerritoryScoringTests
{
    private static readonly PlayerId Self = new(0);
    private static readonly PlayerId Enemy = new(1);

    [Fact]
    public void Facts_counts_friendly_and_hostile_neighbors_and_their_troops()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "WesternAustralia")
            .Owns(Self, 2, "EasternAustralia")
            .Owns(Enemy, 4, "Indonesia")
            .Owns(Enemy, 3, "NewGuinea")
            .Build();

        var facts = TerritoryScoring.Facts(view, Self, new TerritoryId("WesternAustralia"));

        Assert.Equal(3, facts.Troops);
        Assert.Equal(1, facts.FriendlyNeighbors);
        Assert.Equal(2, facts.HostileNeighbors);
        Assert.Equal(7, facts.HostileNeighborTroops);
        Assert.Equal(4, facts.StrongestHostileNeighborTroops);
    }

    [Fact]
    public void IndexOf_matches_the_position_in_WorldMap_Territories()
    {
        var territories = WorldMap.Territories;

        Assert.Equal(0, TerritoryScoring.IndexOf(territories[0].Id));
        Assert.Equal(5, TerritoryScoring.IndexOf(territories[5].Id));
        Assert.Equal(territories.Count - 1, TerritoryScoring.IndexOf(territories[^1].Id));
    }

    [Fact]
    public void DefenseUrgency_matches_the_closed_form_formula()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "WesternAustralia")
            .Owns(Enemy, 4, "Indonesia")
            .Owns(Enemy, 3, "NewGuinea")
            .Build();

        var facts = TerritoryScoring.Facts(view, Self, new TerritoryId("WesternAustralia"));

        Assert.Equal(4, TerritoryScoring.DefenseUrgency(facts));
    }

    [Fact]
    public void DefenseUrgency_is_zero_when_there_are_no_enemy_neighbors()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 5, "Argentina")
            .Unclaimed("Brazil", "Peru")
            .Build();

        var facts = TerritoryScoring.Facts(view, Self, new TerritoryId("Argentina"));

        Assert.Equal(0, TerritoryScoring.DefenseUrgency(facts));
    }

    [Fact]
    public void Owned_returns_only_territories_owned_by_self()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "WesternAustralia", "EasternAustralia")
            .Owns(Enemy, 4, "Indonesia", "NewGuinea")
            .Build();

        var owned = TerritoryScoring.Owned(view, Self);

        Assert.Equivalent(
            new[] { new TerritoryId("WesternAustralia"), new TerritoryId("EasternAustralia") },
            owned,
            strict: true);
    }

    [Fact]
    public void Frontier_returns_only_owned_territories_with_at_least_one_hostile_neighbor()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Congo", "NorthAfrica", "EastAfrica", "SouthAfrica")
            .Owns(Enemy, 2, "Egypt")
            .Build();

        var frontier = TerritoryScoring.Frontier(view, Self);

        Assert.Equivalent(
            new[] { new TerritoryId("NorthAfrica"), new TerritoryId("EastAfrica") },
            frontier,
            strict: true);
    }

    [Fact]
    public void OwnComponents_splits_disconnected_owned_territories_into_separate_groups()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Congo", "NorthAfrica", "EastAfrica", "SouthAfrica")
            .Owns(Self, 1, "Alaska")
            .Owns(Enemy, 2, "Egypt")
            .Build();

        var components = TerritoryScoring.OwnComponents(view, Self);

        Assert.Equal(2, components.Count);
        Assert.Contains(components, c => c.SetEquals(new HashSet<TerritoryId>
        {
            new("Congo"), new("NorthAfrica"), new("EastAfrica"), new("SouthAfrica")
        }));
        Assert.Contains(components, c => c.SetEquals(new HashSet<TerritoryId> { new("Alaska") }));
    }

    [Fact]
    public void ContinentPressure_scales_with_self_owned_share_of_the_continent()
    {
        var lowOwnership = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "WesternAustralia")
            .Owns(Enemy, 1, "Indonesia", "NewGuinea", "EasternAustralia")
            .Build();

        var highOwnership = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "WesternAustralia", "NewGuinea", "EasternAustralia")
            .Owns(Enemy, 1, "Indonesia")
            .Build();

        var low = TerritoryScoring.ContinentPressure(lowOwnership, Self, new TerritoryId("WesternAustralia"));
        var high = TerritoryScoring.ContinentPressure(highOwnership, Self, new TerritoryId("WesternAustralia"));

        // Oceania: bonus 2, 4 members. low = 2*(0+1)/4 = 0.5; high = 2*(2+1)/4 = 1.5.
        Assert.Equal(0.5, low, precision: 5);
        Assert.Equal(1.5, high, precision: 5);
    }

}
