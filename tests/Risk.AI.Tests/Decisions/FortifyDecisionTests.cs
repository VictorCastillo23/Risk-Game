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

    [Fact]
    public void Decide_considers_a_higher_troop_non_safest_source_when_it_yields_a_larger_net_gain()
    {
        // Alaska, NorthwestTerritory, and Alberta are all mutually adjacent (one component).
        // Alaska has the LOWEST urgency (0, no hostile neighbor) but only 2 troops to spare.
        // NorthwestTerritory has nonzero urgency (it borders the same heavy enemy stack) but
        // 8 troops to spare - far more than Alaska could ever contribute to Alberta's defense.
        // The best fortify must come from NorthwestTerritory, not from the "safest" Alaska.
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 2, "Alaska")
            .Owns(Self, 8, "NorthwestTerritory")
            .Owns(Self, 1, "Alberta")
            .Owns(Enemy, 20, "Ontario") // Threatens both NorthwestTerritory and Alberta.
            .Build();

        var command = FortifyDecision.Decide(view, Self);

        // urgency(Alberta) = max(0, 20-1) = 19. From NorthwestTerritory: troops =
        // min(fromTroops-1=7, max(1,19)) = 7; netGain = 19 - max(0,20-(1+7)) = 19-12 = 7.
        // From Alaska: troops = min(1, 19) = 1; netGain = 19 - max(0,20-(1+1)) = 19-18 = 1.
        var fortify = Assert.IsType<FortifyCommand>(command);
        Assert.Equal(new TerritoryId("NorthwestTerritory"), fortify.From);
        Assert.Equal(new TerritoryId("Alberta"), fortify.To);
        Assert.Equal(7, fortify.Troops);
    }
}
