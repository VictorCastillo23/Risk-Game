using Risk.AI.Decisions;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;

namespace Risk.AI.Tests.Decisions;

public class HeadquartersDecisionTests
{
    private static readonly PlayerId Self = new(0);
    private static readonly PlayerId Enemy = new(1);

    [Fact]
    public void Decide_avoids_a_border_territory_when_an_interior_option_exists()
    {
        // WesternAustralia's neighbors are Indonesia, NewGuinea, and EasternAustralia.
        // Indonesia/EasternAustralia are self-owned; NewGuinea is unclaimed (Owner: null),
        // so TerritoryScoring.Facts counts it as neither friendly nor hostile: an interior
        // pick with zero enemy neighbors.
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "WesternAustralia", "EasternAustralia", "Indonesia")
            .Owns(Self, 3, "Alberta")
            .Owns(Enemy, 5, "Alaska", "NorthwestTerritory", "Ontario")
            .Build();

        var command = HeadquartersDecision.Decide(view, Self);

        var select = Assert.IsType<SelectHeadquartersCommand>(command);
        Assert.Equal(new TerritoryId("WesternAustralia"), select.Territory);
        Assert.Equal(Self, select.Actor);
    }

    [Fact]
    public void Decide_prefers_the_territory_within_the_most_valuable_owned_continent()
    {
        // Self fully owns Oceania (bonus 2, 4 members) and also owns one isolated
        // North America territory with zero enemy neighbors itself, but NA's
        // per-member bonus contribution is diluted because self owns nothing else there.
        var view = PlayerViewBuilder.For(Self)
            .OwnsContinent(Self, "OC", 3)
            .Owns(Self, 3, "Yakutsk") // isolated interior Asia pick, but self owns nothing else in Asia (bonus 7 / 12 members).
            .Build();

        var command = HeadquartersDecision.Decide(view, Self);

        var select = Assert.IsType<SelectHeadquartersCommand>(command);
        // Any fully-owned Oceania member scores higher: continent value component is
        // ContinentBonusWeight * bonus * ownedFraction = 1.0 * 2 * (4/4) = 2.0 for Oceania
        // members, vs 1.0 * 7 * (1/12) ≈ 0.58 for Yakutsk.
        Assert.Contains(select.Territory.Value, new[] { "Indonesia", "NewGuinea", "WesternAustralia", "EasternAustralia" });
    }
}
