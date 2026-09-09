using Risk.AI.Scoring;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Rules;

namespace Risk.AI.Tests.Scoring;

public class CardTradeSelectionTests
{
    private static readonly PlayerId Self = new(0);

    private static readonly TerritoryCard AlaskaInfantry = new(new TerritoryId("Alaska"), CardSymbol.Infantry);
    private static readonly TerritoryCard AlbertaInfantry = new(new TerritoryId("Alberta"), CardSymbol.Infantry);
    private static readonly TerritoryCard OntarioInfantry = new(new TerritoryId("Ontario"), CardSymbol.Infantry);
    private static readonly TerritoryCard BrazilCavalry = new(new TerritoryId("Brazil"), CardSymbol.Cavalry);
    private static readonly TerritoryCard EgyptArtillery = new(new TerritoryId("Egypt"), CardSymbol.Artillery);

    [Fact]
    public void BestTrade_returns_a_valid_three_of_a_kind_set()
    {
        var view = PlayerViewBuilder.For(Self)
            .Hand(AlaskaInfantry, AlbertaInfantry, OntarioInfantry)
            .Build();

        var trade = CardTradeSelection.BestTrade(view, Self);

        Assert.NotNull(trade);
        Assert.True(CardSet.IsValid(trade!.Cards));
        Assert.Equal(Self, trade.Actor);
    }

    [Fact]
    public void BestTrade_returns_null_when_no_three_card_subset_of_the_hand_is_valid()
    {
        var cavalry2 = new TerritoryCard(new TerritoryId("Peru"), CardSymbol.Cavalry);
        var view = PlayerViewBuilder.For(Self)
            .Hand(AlaskaInfantry, AlbertaInfantry, BrazilCavalry, cavalry2)
            .Build();

        var trade = CardTradeSelection.BestTrade(view, Self);

        Assert.Null(trade);
    }

    [Fact]
    public void BestTrade_rejects_a_hand_of_two_wildcards_plus_one_symbol()
    {
        var view = PlayerViewBuilder.For(Self)
            .Hand(new WildCard(), new WildCard(), AlaskaInfantry)
            .Build();

        var trade = CardTradeSelection.BestTrade(view, Self);

        Assert.Null(trade);
    }

    [Fact]
    public void BestTrade_picks_a_valid_set_out_of_a_larger_hand_when_one_exists()
    {
        var view = PlayerViewBuilder.For(Self)
            .Hand(AlaskaInfantry, AlbertaInfantry, BrazilCavalry, EgyptArtillery)
            .Build();

        var trade = CardTradeSelection.BestTrade(view, Self);

        Assert.NotNull(trade);
        Assert.True(CardSet.IsValid(trade!.Cards));
    }

    [Fact]
    public void BestTrade_sets_the_bonus_territory_when_one_traded_card_names_a_territory_self_owns()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 1, "Alaska")
            .Hand(AlaskaInfantry, AlbertaInfantry, OntarioInfantry)
            .Build();

        var trade = CardTradeSelection.BestTrade(view, Self);

        Assert.NotNull(trade);
        Assert.Equal(new TerritoryId("Alaska"), trade!.BonusTerritory);
    }

    [Fact]
    public void BestTrade_leaves_the_bonus_territory_null_when_self_owns_none_of_the_named_territories()
    {
        var view = PlayerViewBuilder.For(Self)
            .Hand(AlaskaInfantry, AlbertaInfantry, OntarioInfantry)
            .Build();

        var trade = CardTradeSelection.BestTrade(view, Self);

        Assert.NotNull(trade);
        Assert.Null(trade!.BonusTerritory);
    }
}
