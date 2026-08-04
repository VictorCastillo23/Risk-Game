using Risk.Domain.Cards;

namespace Risk.Tests.Domain;

public class DeckTests
{
    [Fact]
    public void CreateStandard_returns_44_cards()
    {
        var deck = Deck.CreateStandard();

        Assert.Equal(44, deck.Count);
    }

    [Fact]
    public void CreateStandard_has_14_of_each_symbol_and_2_wildcards()
    {
        var deck = Deck.CreateStandard();

        var infantry = deck.OfType<TerritoryCard>().Count(c => c.Symbol == CardSymbol.Infantry);
        var cavalry = deck.OfType<TerritoryCard>().Count(c => c.Symbol == CardSymbol.Cavalry);
        var artillery = deck.OfType<TerritoryCard>().Count(c => c.Symbol == CardSymbol.Artillery);
        var wildcards = deck.OfType<WildCard>().Count();

        Assert.Equal(14, infantry);
        Assert.Equal(14, cavalry);
        Assert.Equal(14, artillery);
        Assert.Equal(2, wildcards);
    }

    [Fact]
    public void CreateStandard_assigns_42_distinct_territory_ids()
    {
        var deck = Deck.CreateStandard();

        var distinctTerritories = deck.OfType<TerritoryCard>().Select(c => c.Territory).Distinct().Count();

        Assert.Equal(42, distinctTerritories);
    }
}
