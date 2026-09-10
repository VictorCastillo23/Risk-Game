using Risk.AI.Scoring;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Map;
using Risk.Domain.Players;

namespace Risk.AI.Tests.Scoring;

public class CapitalScoringTests
{
    private static readonly PlayerId Self = new(0);
    private static readonly PlayerId Enemy = new(1);
    private static readonly PlayerId OtherEnemy = new(2);

    [Fact]
    public void GainForCapturing_is_zero_outside_capital_mode()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 2, "Alberta")
            .Build();

        Assert.Equal(0, CapitalScoring.GainForCapturing(view, Self, new TerritoryId("Alberta")));
    }

    [Fact]
    public void GainForDefending_is_zero_outside_capital_mode()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Build();

        Assert.Equal(0, CapitalScoring.GainForDefending(view, Self, new TerritoryId("Alaska")));
    }

    [Fact]
    public void Both_gains_are_zero_when_own_headquarters_is_not_yet_selected()
    {
        // Capital mode, pre-selection: OwnHeadquarters is null even though the mode is
        // active (SelectHeadquarters phase not yet completed by this viewer).
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 2, "Alberta")
            .Build();

        Assert.Equal(0, CapitalScoring.GainForCapturing(view, Self, new TerritoryId("Alberta")));
        Assert.Equal(0, CapitalScoring.GainForDefending(view, Self, new TerritoryId("Alaska")));
    }

    [Fact]
    public void GainForDefending_raises_weight_when_own_headquarters_is_threatened_before_reveal()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 2, "Alaska")
            .Owns(Enemy, 5, "Alberta")
            .OwnHeadquarters("Alaska")
            .Build();

        Assert.True(view.RevealedHeadquarters.Count == 0);
        Assert.True(CapitalScoring.GainForDefending(view, Self, new TerritoryId("Alaska")) > 0);
    }

    [Fact]
    public void GainForDefending_is_zero_when_own_headquarters_is_not_threatened()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 5, "Alaska")
            .Owns(Enemy, 2, "Alberta")
            .OwnHeadquarters("Alaska")
            .Build();

        Assert.Equal(0, CapitalScoring.GainForDefending(view, Self, new TerritoryId("Alaska")));
    }

    [Fact]
    public void GainForDefending_is_zero_for_a_territory_that_is_not_the_headquarters()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 2, "Alaska", "NorthwestTerritory")
            .Owns(Enemy, 5, "Alberta")
            .OwnHeadquarters("Alaska")
            .Build();

        // NorthwestTerritory is just as threatened as Alaska but is not the HQ.
        Assert.Equal(0, CapitalScoring.GainForDefending(view, Self, new TerritoryId("NorthwestTerritory")));
    }

    [Fact]
    public void GainForCapturing_is_zero_before_all_headquarters_are_revealed()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 4, "Alberta")
            .OwnHeadquarters("Alaska")
            .Build();

        Assert.Equal(0, CapitalScoring.GainForCapturing(view, Self, new TerritoryId("Alberta")));
    }

    [Fact]
    public void GainForCapturing_favors_the_weakest_revealed_enemy_headquarters()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 2, "Alberta")
            .Owns(OtherEnemy, 8, "Ukraine")
            .OwnHeadquarters("Alaska")
            .RevealedHeadquarters((Self, "Alaska"), (Enemy, "Alberta"), (OtherEnemy, "Ukraine"))
            .Build();

        var weakest = CapitalScoring.GainForCapturing(view, Self, new TerritoryId("Alberta"));
        var strongest = CapitalScoring.GainForCapturing(view, Self, new TerritoryId("Ukraine"));

        Assert.True(weakest > strongest, $"Expected weakest HQ weight ({weakest}) to exceed strongest HQ weight ({strongest})");
        Assert.True(strongest > 0);
    }

    [Fact]
    public void GainForCapturing_is_zero_for_an_enemy_territory_that_is_not_a_headquarters()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 2, "Alberta")
            .Owns(Enemy, 3, "NorthwestTerritory")
            .OwnHeadquarters("Alaska")
            .RevealedHeadquarters((Self, "Alaska"), (Enemy, "Alberta"))
            .Build();

        Assert.Equal(0, CapitalScoring.GainForCapturing(view, Self, new TerritoryId("NorthwestTerritory")));
    }

    [Fact]
    public void GainForCapturing_prioritizes_recapturing_a_lost_own_headquarters_above_hunting_any_enemy_hq()
    {
        // Alaska is self's OwnHeadquarters, captured by Enemy. Enemy's OWN declared HQ is
        // the separate territory Alberta; OtherEnemy's declared HQ, Ukraine, is garrisoned
        // at just 1 troop — the weakest possible garrison, which gives the generic
        // hunt-weakest-HQ formula its maximum possible score
        // (BotWeights.EnemyHqCaptureWeight / 1). Recapturing your OWN lost HQ must still
        // outscore that ceiling, since CapitalVictoryRule makes holding it a hard
        // precondition for winning regardless of how many enemy HQs are held.
        var view = PlayerViewBuilder.For(Self)
            .Owns(Enemy, 1, "Alaska")
            .Owns(Enemy, 4, "Alberta")
            .Owns(OtherEnemy, 1, "Ukraine")
            .OwnHeadquarters("Alaska")
            .RevealedHeadquarters((Self, "Alaska"), (Enemy, "Alberta"), (OtherEnemy, "Ukraine"))
            .Build();

        var recaptureWeight = CapitalScoring.GainForCapturing(view, Self, new TerritoryId("Alaska"));
        var maxPossibleHuntWeight = CapitalScoring.GainForCapturing(view, Self, new TerritoryId("Ukraine"));

        Assert.True(recaptureWeight > 0);
        Assert.True(recaptureWeight > maxPossibleHuntWeight,
            $"Expected recapture weight ({recaptureWeight}) to exceed the hunt-weight ceiling ({maxPossibleHuntWeight})");
    }

    [Fact]
    public void GainForCapturing_does_not_apply_recapture_priority_when_self_still_owns_its_own_headquarters()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 2, "Alberta")
            .OwnHeadquarters("Alaska")
            .Build();

        // Pre-reveal, no threat data implies this: self's own HQ, still self-owned, is
        // not itself an attack target, so it must fall through to the normal (zero)
        // pre-reveal capturing result, not the recapture-priority branch.
        Assert.Equal(0, CapitalScoring.GainForCapturing(view, Self, new TerritoryId("Alaska")));
    }
}
