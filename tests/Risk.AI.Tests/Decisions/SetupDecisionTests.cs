using Risk.AI.Decisions;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.State;

namespace Risk.AI.Tests.Decisions;

public class SetupDecisionTests
{
    private static readonly PlayerId Self = new(0);
    private static readonly PlayerId Opponent = new(1);
    private static readonly PlayerId Neutral = new(2);

    [Fact]
    public void Decide_places_on_the_owned_territory_with_the_highest_defense_urgency()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Self, 2, "NorthwestTerritory")
            .Owns(Opponent, 7, "Alberta")
            .Phase(TurnPhase.Setup)
            .Build();

        var command = SetupDecision.Decide(view, Self, BotMemory.Empty);

        // NorthwestTerritory borders Alberta (7 troops) with only 2 defenders: urgency 5.
        // Alaska borders no enemy: urgency 0.
        var placement = Assert.IsType<PlaceTroopsCommand>(command);
        Assert.Equal(new TerritoryId("NorthwestTerritory"), placement.Territory);
        Assert.Equal(1, placement.Troops);
        Assert.Equal(Self, placement.Actor);
    }

    [Fact]
    public void Decide_does_not_switch_to_neutral_placement_outside_the_two_player_shape()
    {
        // partyCount == 2 (no third party at all), so the TwoPlayer Phase-B predicate can never hold.
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 1, "Alaska")
            .Owns(Opponent, 5, "Alberta")
            .OtherHandCount(Opponent, 3)
            .Phase(TurnPhase.Setup)
            .Build();

        var memory = BotMemory.Empty
            .WithSeenActor(Self)
            .WithSeenActor(Opponent) with
        { OwnSetupTroopsPlaced = 26 };

        var command = SetupDecision.Decide(view, Self, memory);

        Assert.IsType<PlaceTroopsCommand>(command);
    }

    [Fact]
    public void Decide_switches_to_neutral_placement_at_the_two_player_phase_b_boundary()
    {
        // partyCount == 3 (self, opponent, neutral), OwnEffectiveMission null (default),
        // SeenActors == {self, opponent} (neutral never becomes CurrentPlayer),
        // OwnSetupTroopsPlaced >= 26 (TwoPlayer's own Setup pool: 40 starting - 14 dealt).
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 1, "Alaska")
            .Owns(Opponent, 5, "Alberta")
            .Owns(Neutral, 1, "Ontario", "Quebec")
            .OtherHandCount(Opponent, 3)
            .OtherHandCount(Neutral, 0)
            .Phase(TurnPhase.Setup)
            .Build();

        var memory = BotMemory.Empty
            .WithSeenActor(Self)
            .WithSeenActor(Opponent) with
        { OwnSetupTroopsPlaced = 26 };

        var command = SetupDecision.Decide(view, Self, memory);

        Assert.IsType<PlaceNeutralTroopsCommand>(command);
        var placement = (PlaceNeutralTroopsCommand)command;
        Assert.Equal(Self, placement.Actor);
        Assert.Equal(1, placement.Troops);
        Assert.Equal(Neutral, view.Territories[placement.Territory].Owner);
    }

    [Fact]
    public void Decide_does_not_switch_to_neutral_placement_before_the_own_setup_troops_boundary()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 1, "Alaska")
            .Owns(Opponent, 5, "Alberta")
            .Owns(Neutral, 1, "Ontario", "Quebec")
            .OtherHandCount(Opponent, 3)
            .OtherHandCount(Neutral, 0)
            .Phase(TurnPhase.Setup)
            .Build();

        var memory = BotMemory.Empty
            .WithSeenActor(Self)
            .WithSeenActor(Opponent) with
        { OwnSetupTroopsPlaced = 25 };

        var command = SetupDecision.Decide(view, Self, memory);

        Assert.IsType<PlaceTroopsCommand>(command);
    }

    [Fact]
    public void Decide_prefers_the_neutral_territory_bordering_the_opponent_over_one_bordering_self()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 1, "Alberta")
            .Owns(Opponent, 5, "Ontario")
            .Owns(Neutral, 1, "NorthwestTerritory", "Quebec")
            .OtherHandCount(Opponent, 3)
            .OtherHandCount(Neutral, 0)
            .Phase(TurnPhase.Setup)
            .Build();

        var memory = BotMemory.Empty
            .WithSeenActor(Self)
            .WithSeenActor(Opponent) with
        { OwnSetupTroopsPlaced = 26 };

        var command = SetupDecision.Decide(view, Self, memory);

        // Quebec borders Ontario (opponent) only; NorthwestTerritory borders Alberta (self) only.
        var placement = Assert.IsType<PlaceNeutralTroopsCommand>(command);
        Assert.Equal(new TerritoryId("Quebec"), placement.Territory);
    }
}
