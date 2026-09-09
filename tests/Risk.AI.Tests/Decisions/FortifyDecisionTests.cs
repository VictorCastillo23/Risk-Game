using Risk.AI.Decisions;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;

namespace Risk.AI.Tests.Decisions;

public class FortifyDecisionTests
{
    private static readonly PlayerId Self = new(0);
    private static readonly PlayerId Enemy = new(1);

    [Fact]
    public void Decide_ends_the_phase_when_fortify_was_already_used_this_turn()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 5, "Alaska")
            .Owns(Self, 1, "NorthwestTerritory")
            .Owns(Enemy, 5, "Alberta")
            .FortifyUsed()
            .Build();

        var command = FortifyDecision.Decide(view, Self);

        Assert.IsType<EndPhaseCommand>(command);
    }

    [Fact]
    public void Decide_ends_the_phase_when_no_connected_pair_can_improve_defense()
    {
        // Alaska (safe, 2 troops, no hostile neighbor) is the only viable source; its sole
        // same-component owned neighbor, NorthwestTerritory, has no hostile neighbor either,
        // so no move raises anything's defense urgency.
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 2, "Alaska")
            .Owns(Self, 1, "NorthwestTerritory")
            .Build();

        var command = FortifyDecision.Decide(view, Self);

        Assert.IsType<EndPhaseCommand>(command);
    }

    [Fact]
    public void Decide_ends_the_phase_when_the_only_urgent_territory_is_not_connected_to_any_safe_source()
    {
        // Alaska and Peru are on opposite sides of the real WorldMap graph with no chain of
        // self-owned territory between them: each is its own single-territory component. Peru
        // is threatened but has only 1 troop (can't act as a source); Alaska has troops to
        // spare but no hostile neighbor of its own (not a fortify target).
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 5, "Alaska")
            .Owns(Self, 1, "Peru")
            .Owns(Enemy, 5, "Brazil") // Threatens Peru only.
            .Build();

        var command = FortifyDecision.Decide(view, Self);

        Assert.IsType<EndPhaseCommand>(command);
    }

    [Fact]
    public void Decide_fortifies_the_most_urgent_connected_territory_from_the_safest_source()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 6, "NorthwestTerritory") // Safe interior source, no hostile neighbor.
            .Owns(Self, 1, "Alaska") // Threatened, connected to NorthwestTerritory.
            .Owns(Enemy, 8, "Kamchatka") // Threatens Alaska only.
            .Build();

        var command = FortifyDecision.Decide(view, Self);

        // urgency(Alaska) = max(0, 8 - 1) = 7; troops = min(fromTroops-1=5, max(1,7)) = 5.
        var fortify = Assert.IsType<FortifyCommand>(command);
        Assert.Equal(Self, fortify.Actor);
        Assert.Equal(new TerritoryId("NorthwestTerritory"), fortify.From);
        Assert.Equal(new TerritoryId("Alaska"), fortify.To);
        Assert.Equal(5, fortify.Troops);
    }
}
