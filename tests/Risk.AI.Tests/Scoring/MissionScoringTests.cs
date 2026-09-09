using Risk.AI.Scoring;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;

namespace Risk.AI.Tests.Scoring;

public class MissionScoringTests
{
    private static readonly PlayerId Self = new(0);
    private static readonly PlayerId Enemy = new(1);
    private static readonly PlayerId OtherEnemy = new(2);

    [Fact]
    public void GainForCapturing_is_zero_when_there_is_no_effective_mission()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 2, "Alberta")
            .Build();

        Assert.Equal(0, MissionScoring.GainForCapturing(view, Self, new TerritoryId("Alberta")));
    }

    [Fact]
    public void GainForReinforcing_is_zero_when_there_is_no_effective_mission()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Build();

        Assert.Equal(0, MissionScoring.GainForReinforcing(view, Self, new TerritoryId("Alaska"), 2));
    }

    [Fact]
    public void OccupyTerritories_capturing_gain_is_flat_weight_while_quota_unmet()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 2, "Alaska", "Alberta")
            .Owns(Enemy, 2, "Ontario")
            .Mission(new OccupyTerritories(3, MinArmiesPerTerritory: 1))
            .Build();

        // Self owns 2 qualifying territories, quota is 3 -> still below quota.
        Assert.Equal(2.0, MissionScoring.GainForCapturing(view, Self, new TerritoryId("Ontario")));
    }

    [Fact]
    public void OccupyTerritories_capturing_gain_is_zero_once_quota_is_met()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 2, "Alaska", "Alberta", "Ontario")
            .Owns(Enemy, 2, "Quebec")
            .Mission(new OccupyTerritories(3, MinArmiesPerTerritory: 1))
            .Build();

        // Self already owns 3 qualifying territories == quota.
        Assert.Equal(0, MissionScoring.GainForCapturing(view, Self, new TerritoryId("Quebec")));
    }

    [Fact]
    public void OccupyTerritories_reinforcing_gain_rewards_crossing_the_min_armies_threshold()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 1, "Alaska")
            .Mission(new OccupyTerritories(18, MinArmiesPerTerritory: 2))
            .Build();

        // 1 troop + 2 placed = 3, crosses the MinArmiesPerTerritory=2 threshold.
        Assert.Equal(2.0, MissionScoring.GainForReinforcing(view, Self, new TerritoryId("Alaska"), 2));
    }

    [Fact]
    public void OccupyTerritories_reinforcing_gain_is_zero_when_min_armies_is_one()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 1, "Alaska")
            .Mission(new OccupyTerritories(18, MinArmiesPerTerritory: 1))
            .Build();

        Assert.Equal(0, MissionScoring.GainForReinforcing(view, Self, new TerritoryId("Alaska"), 2));
    }

    [Fact]
    public void OccupyTerritories_reinforcing_gain_is_zero_when_placement_does_not_cross_threshold()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 5, "Alaska")
            .Mission(new OccupyTerritories(18, MinArmiesPerTerritory: 2))
            .Build();

        // Already at/above threshold before placing -> no crossing event.
        Assert.Equal(0, MissionScoring.GainForReinforcing(view, Self, new TerritoryId("Alaska"), 2));
    }

    [Fact]
    public void ConquerContinents_capturing_gain_favors_the_near_complete_required_continent()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "WesternAustralia", "EasternAustralia", "NewGuinea")
            .Owns(Enemy, 1, "Indonesia")
            .Owns(Enemy, 1, "Venezuela")
            .Mission(new ConquerContinents([new ContinentId("OC")]))
            .Build();

        var insideRequired = MissionScoring.GainForCapturing(view, Self, new TerritoryId("Indonesia"));
        var outsideRequired = MissionScoring.GainForCapturing(view, Self, new TerritoryId("Venezuela"));

        Assert.True(insideRequired > outsideRequired,
            $"Expected inside-required weight ({insideRequired}) to exceed outside-required weight ({outsideRequired})");
        Assert.Equal(0, outsideRequired);
    }

    [Fact]
    public void ConquerContinents_capturing_gain_scores_the_deterministic_wildcard_continent()
    {
        // Required: NA, AF. Wildcard slot: 1. Self already owns most of Oceania (4 members,
        // owns 3), and none of Europe -> Oceania is the highest owned/size ratio among the
        // non-required continents, so it is the deterministic wildcard pick.
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "WesternAustralia", "EasternAustralia", "NewGuinea")
            .Owns(Enemy, 1, "Indonesia")
            .Owns(Enemy, 1, "GreatBritain")
            .Mission(new ConquerContinents([new ContinentId("NA"), new ContinentId("AF")], WildcardCount: 1))
            .Build();

        var wildcardTarget = MissionScoring.GainForCapturing(view, Self, new TerritoryId("Indonesia"));
        var nonWildcardTarget = MissionScoring.GainForCapturing(view, Self, new TerritoryId("GreatBritain"));

        Assert.True(wildcardTarget > 0);
        Assert.Equal(0, nonWildcardTarget);
    }

    [Fact]
    public void ConquerContinents_capturing_gain_is_zero_for_non_required_continent_with_no_wildcard_slot()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 1, "Indonesia")
            .Mission(new ConquerContinents([new ContinentId("NA")]))
            .Build();

        Assert.Equal(0, MissionScoring.GainForCapturing(view, Self, new TerritoryId("Indonesia")));
    }

    [Fact]
    public void ConquerContinents_reinforcing_gain_is_always_zero()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Mission(new ConquerContinents([new ContinentId("NA")]))
            .Build();

        Assert.Equal(0, MissionScoring.GainForReinforcing(view, Self, new TerritoryId("Alaska"), 2));
    }

    [Fact]
    public void EliminateArmy_capturing_gain_increases_as_the_target_players_remaining_territories_decrease()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 2, "Alberta")
            .Owns(OtherEnemy, 2, "Ontario", "Quebec", "Greenland")
            .Mission(new EliminateArmy(new ArmyId(Enemy.Value)))
            .Build();

        // Enemy holds only 1 territory (Alberta); scoring a candidate against OtherEnemy's
        // territory (not the resolved target) must be zero.
        var againstResolvedTarget = MissionScoring.GainForCapturing(view, Self, new TerritoryId("Alberta"));
        var againstOtherPlayer = MissionScoring.GainForCapturing(view, Self, new TerritoryId("Ontario"));

        Assert.True(againstResolvedTarget > 0);
        Assert.Equal(0, againstOtherPlayer);
    }

    [Fact]
    public void EliminateArmy_capturing_gain_is_higher_when_the_target_is_closer_to_elimination()
    {
        var nearlyEliminated = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 2, "Alberta")
            .Mission(new EliminateArmy(new ArmyId(Enemy.Value)))
            .Build();

        var farFromEliminated = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 2, "Alberta", "Ontario", "Quebec", "Greenland")
            .Mission(new EliminateArmy(new ArmyId(Enemy.Value)))
            .Build();

        var nearWeight = MissionScoring.GainForCapturing(nearlyEliminated, Self, new TerritoryId("Alberta"));
        var farWeight = MissionScoring.GainForCapturing(farFromEliminated, Self, new TerritoryId("Alberta"));

        Assert.True(nearWeight > farWeight,
            $"Expected near-elimination weight ({nearWeight}) to exceed far-from-elimination weight ({farWeight})");
    }

    [Fact]
    public void EliminateArmy_never_targets_the_bots_own_seat()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 2, "Alberta")
            .Mission(new EliminateArmy(new ArmyId(Self.Value)))
            .Build();

        Assert.Equal(0, MissionScoring.GainForCapturing(view, Self, new TerritoryId("Alberta")));
        Assert.Equal(0, MissionScoring.GainForCapturing(view, Self, new TerritoryId("Alaska")));
    }

    [Fact]
    public void EliminateArmy_reinforcing_gain_is_always_zero()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Mission(new EliminateArmy(new ArmyId(Enemy.Value)))
            .Build();

        Assert.Equal(0, MissionScoring.GainForReinforcing(view, Self, new TerritoryId("Alaska"), 2));
    }
}
