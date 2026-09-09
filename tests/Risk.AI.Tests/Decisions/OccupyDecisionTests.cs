using Risk.AI.Decisions;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;

namespace Risk.AI.Tests.Decisions;

public class OccupyDecisionTests
{
    private static readonly PlayerId Self = new(0);
    private static readonly PlayerId Enemy = new(1);

    [Fact]
    public void Decide_clamps_to_the_minimum_when_the_source_has_exactly_one_troop_above_the_minimum()
    {
        // D7 boundary: fromTroops - 1 == MinimumTroops, so min == max regardless of share.
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska") // fromTroops - 1 = 2 == MinimumTroops.
            .Owns(Self, 0, "Alberta") // Conquered, per the engine's post-conquest state.
            .Owns(Enemy, 9, "NorthwestTerritory") // Heavy exposure on the source, still can't push above max.
            .PendingOccupation("Alaska", "Alberta", 2)
            .Build();

        var command = OccupyDecision.Decide(view, Self);

        var occupy = Assert.IsType<OccupyCommand>(command);
        Assert.Equal(Self, occupy.Actor);
        Assert.Equal(2, occupy.Troops);
    }

    [Fact]
    public void Decide_clamps_to_the_maximum_when_the_conquered_territory_is_far_more_exposed()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 10, "Alaska")
            .Owns(Self, 0, "Alberta")
            .Owns(Enemy, 20, "Ontario") // Heavily threatens the newly-conquered Alberta.
            .PendingOccupation("Alaska", "Alberta", 1)
            .Build();

        var command = OccupyDecision.Decide(view, Self);

        // max = fromTroops(10) - 1 = 9. Alberta's exposure vastly outweighs Alaska's (which
        // has no hostile neighbor at all here), so share -> 1 and troops clamp to the max.
        var occupy = Assert.IsType<OccupyCommand>(command);
        Assert.Equal(9, occupy.Troops);
    }

    [Fact]
    public void Decide_stays_near_the_minimum_when_the_source_is_far_more_exposed_than_the_conquered_territory()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 10, "Alaska")
            .Owns(Self, 0, "Alberta")
            // Kamchatka borders ONLY Alaska (not Alberta), isolating the source's exposure.
            .Owns(Enemy, 20, "Kamchatka")
            .PendingOccupation("Alaska", "Alberta", 1)
            .Build();

        var command = OccupyDecision.Decide(view, Self);

        // Alberta itself has no hostile neighbor in this setup (NorthwestTerritory/Ontario/
        // WesternUnitedStates all unclaimed), so share -> 0 and troops clamp to the minimum.
        var occupy = Assert.IsType<OccupyCommand>(command);
        Assert.Equal(1, occupy.Troops);
    }

    [Fact]
    public void Decide_stays_within_bounds_for_a_generously_sized_source_with_balanced_exposure()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 8, "Alaska")
            .Owns(Self, 0, "Alberta")
            .Owns(Enemy, 4, "NorthwestTerritory")
            .Owns(Enemy, 4, "Ontario")
            .PendingOccupation("Alaska", "Alberta", 1)
            .Build();

        var command = OccupyDecision.Decide(view, Self);

        var occupy = Assert.IsType<OccupyCommand>(command);
        Assert.InRange(occupy.Troops, 1, 7); // min=1, max=fromTroops(8)-1=7.
    }
}
