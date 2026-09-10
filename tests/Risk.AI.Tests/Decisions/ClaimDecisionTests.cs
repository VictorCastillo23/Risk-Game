using Risk.AI.Decisions;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.State;

namespace Risk.AI.Tests.Decisions;

public class ClaimDecisionTests
{
    private static readonly PlayerId Self = new(0);
    private static readonly PlayerId Enemy = new(1);

    [Fact]
    public void Decide_claims_the_territory_that_would_complete_a_continent_over_an_unrelated_one()
    {
        const string completing = "Indonesia"; // Oceania; self already owns the other 3 members.
        const string nonCompleting = "Kamchatka"; // Asia; self owns nothing there.

        var others = OtherTerritoryNames(completing, nonCompleting, "WesternAustralia", "EasternAustralia", "NewGuinea");

        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "WesternAustralia", "EasternAustralia", "NewGuinea")
            .Owns(Enemy, 1, others)
            .Unclaimed(completing, nonCompleting)
            .Phase(TurnPhase.Claim)
            .Build();

        var command = ClaimDecision.Decide(view, Self);

        var claim = Assert.IsType<ClaimTerritoryCommand>(command);
        Assert.Equal(new TerritoryId(completing), claim.Territory);
        Assert.Equal(Self, claim.Actor);
        Assert.Equal(1, claim.Troops);
    }

    [Fact]
    public void Decide_prefers_the_unclaimed_territory_adjacent_to_more_of_self_owned_territory()
    {
        const string adjacentToSelf = "Alaska"; // Neighbors Alberta, which self owns.
        const string farFromSelf = "Peru"; // Not adjacent to Alberta or any self-owned territory.

        var others = OtherTerritoryNames(adjacentToSelf, farFromSelf, "Alberta");

        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alberta")
            .Owns(Enemy, 1, others)
            .Unclaimed(adjacentToSelf, farFromSelf)
            .Phase(TurnPhase.Claim)
            .Build();

        var command = ClaimDecision.Decide(view, Self);

        var claim = Assert.IsType<ClaimTerritoryCommand>(command);
        Assert.Equal(new TerritoryId(adjacentToSelf), claim.Territory);
    }

    private static string[] OtherTerritoryNames(params string[] excluded) =>
        WorldMap.Territories
            .Select(t => t.Id.Value)
            .Where(name => !excluded.Contains(name))
            .ToArray();
}
