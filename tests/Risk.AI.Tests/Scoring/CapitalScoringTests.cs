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
}
