using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.AI.Tests.Fakes;

public class PlayerViewBuilderTests
{
    private static readonly PlayerId Viewer = new(0);
    private static readonly PlayerId Enemy = new(1);

    [Fact]
    public void Build_includes_all_42_territories_by_default_as_unowned()
    {
        var view = PlayerViewBuilder.For(Viewer).Build();

        Assert.Equal(42, view.Territories.Count);
        Assert.All(view.Territories.Values, t =>
        {
            Assert.Null(t.Owner);
            Assert.Equal(0, t.Troops);
        });
    }

    [Fact]
    public void Build_defaults_turn_to_reinforce_with_viewer_as_current_player()
    {
        var view = PlayerViewBuilder.For(Viewer).Build();

        Assert.Equal(TurnPhase.Reinforce, view.Turn.Phase);
        Assert.Equal(Viewer, view.Turn.CurrentPlayer);
        Assert.False(view.Turn.FortifyUsed);
        Assert.False(view.Turn.MandatoryTradeDown);
        Assert.Null(view.Turn.PendingOccupation);
    }

    [Fact]
    public void Build_defaults_hand_counts_headquarters_and_mission_to_empty_or_null()
    {
        var view = PlayerViewBuilder.For(Viewer).Build();

        Assert.Empty(view.OwnHand);
        Assert.Empty(view.OtherPlayersCardCounts);
        Assert.Null(view.OwnHeadquarters);
        Assert.Empty(view.RevealedHeadquarters);
        Assert.Null(view.OwnEffectiveMission);
    }

    [Fact]
    public void Owns_assigns_owner_and_troops_to_the_named_territories()
    {
        var view = PlayerViewBuilder.For(Viewer)
            .Owns(Viewer, 5, "Alaska", "Alberta")
            .Build();

        Assert.Equal(new TerritoryState(Viewer, 5), view.Territories[new TerritoryId("Alaska")]);
        Assert.Equal(new TerritoryState(Viewer, 5), view.Territories[new TerritoryId("Alberta")]);
    }

    [Fact]
    public void OwnsContinent_assigns_owner_and_troops_to_every_territory_in_that_continent()
    {
        var view = PlayerViewBuilder.For(Viewer)
            .OwnsContinent(Viewer, "SA", 3)
            .Build();

        var southAmerica = WorldMap.Territories.Where(t => t.ContinentId == new ContinentId("SA"));
        Assert.NotEmpty(southAmerica);
        Assert.All(southAmerica, t =>
            Assert.Equal(new TerritoryState(Viewer, 3), view.Territories[t.Id]));
    }

    [Fact]
    public void Unclaimed_resets_named_territories_to_unowned_with_zero_troops()
    {
        var view = PlayerViewBuilder.For(Viewer)
            .Owns(Viewer, 5, "Alaska")
            .Unclaimed("Alaska")
            .Build();

        Assert.Equal(new TerritoryState(null, 0), view.Territories[new TerritoryId("Alaska")]);
    }

    [Fact]
    public void Hand_sets_the_viewers_own_hand()
    {
        var card = new WildCard();

        var view = PlayerViewBuilder.For(Viewer).Hand(card).Build();

        Assert.Equal(new Card[] { card }, view.OwnHand);
    }

    [Fact]
    public void OtherHandCount_sets_the_count_for_that_other_player()
    {
        var view = PlayerViewBuilder.For(Viewer).OtherHandCount(Enemy, 4).Build();

        Assert.Equal(4, view.OtherPlayersCardCounts[Enemy]);
    }

    [Fact]
    public void Phase_overrides_the_default_turn_phase()
    {
        var view = PlayerViewBuilder.For(Viewer).Phase(TurnPhase.Attack).Build();

        Assert.Equal(TurnPhase.Attack, view.Turn.Phase);
    }

    [Fact]
    public void CurrentPlayer_overrides_the_default_current_player()
    {
        var view = PlayerViewBuilder.For(Viewer).CurrentPlayer(Enemy).Build();

        Assert.Equal(Enemy, view.Turn.CurrentPlayer);
    }

    [Fact]
    public void PendingOccupation_sets_the_turns_pending_occupation()
    {
        var view = PlayerViewBuilder.For(Viewer)
            .PendingOccupation("Alaska", "Alberta", 2)
            .Build();

        Assert.Equal(
            new PendingOccupation(new TerritoryId("Alaska"), new TerritoryId("Alberta"), 2),
            view.Turn.PendingOccupation);
    }

    [Fact]
    public void MandatoryTradeDown_sets_the_flag()
    {
        var view = PlayerViewBuilder.For(Viewer).MandatoryTradeDown().Build();

        Assert.True(view.Turn.MandatoryTradeDown);
    }

    [Fact]
    public void FortifyUsed_sets_the_flag()
    {
        var view = PlayerViewBuilder.For(Viewer).FortifyUsed().Build();

        Assert.True(view.Turn.FortifyUsed);
    }

    [Fact]
    public void OwnHeadquarters_sets_the_viewers_headquarters()
    {
        var view = PlayerViewBuilder.For(Viewer).OwnHeadquarters("Ontario").Build();

        Assert.Equal(new TerritoryId("Ontario"), view.OwnHeadquarters);
    }

    [Fact]
    public void RevealedHeadquarters_sets_every_players_headquarters()
    {
        var view = PlayerViewBuilder.For(Viewer)
            .RevealedHeadquarters((Viewer, "Ontario"), (Enemy, "Egypt"))
            .Build();

        Assert.Equal(new TerritoryId("Ontario"), view.RevealedHeadquarters[Viewer]);
        Assert.Equal(new TerritoryId("Egypt"), view.RevealedHeadquarters[Enemy]);
    }

    [Fact]
    public void Mission_sets_the_own_effective_mission()
    {
        var mission = new OccupyTerritories(18, 2);

        var view = PlayerViewBuilder.For(Viewer).Mission(mission).Build();

        Assert.Same(mission, view.OwnEffectiveMission);
    }
}
