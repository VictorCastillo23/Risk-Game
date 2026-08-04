using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Engine.Rules;

namespace Risk.Tests.Rules;

public class CardSetTests
{
    private static readonly TerritoryCard Infantry1 = new(new TerritoryId("Alaska"), CardSymbol.Infantry);
    private static readonly TerritoryCard Infantry2 = new(new TerritoryId("Alberta"), CardSymbol.Infantry);
    private static readonly TerritoryCard Infantry3 = new(new TerritoryId("Ontario"), CardSymbol.Infantry);
    private static readonly TerritoryCard Cavalry1 = new(new TerritoryId("Brazil"), CardSymbol.Cavalry);
    private static readonly TerritoryCard Artillery1 = new(new TerritoryId("Egypt"), CardSymbol.Artillery);
    private static readonly WildCard Wild1 = new();
    private static readonly WildCard Wild2 = new();

    [Fact]
    public void IsValid_accepts_three_of_the_same_symbol()
    {
        Assert.True(CardSet.IsValid([Infantry1, Infantry2, Infantry3]));
    }

    [Fact]
    public void IsValid_accepts_one_of_each_symbol()
    {
        Assert.True(CardSet.IsValid([Infantry1, Cavalry1, Artillery1]));
    }

    [Fact]
    public void IsValid_rejects_two_of_one_symbol_and_one_of_another_without_a_wildcard()
    {
        Assert.False(CardSet.IsValid([Infantry1, Infantry2, Cavalry1]));
    }

    [Fact]
    public void IsValid_accepts_two_matching_symbols_plus_a_wildcard_as_three_of_a_kind()
    {
        Assert.True(CardSet.IsValid([Infantry1, Infantry2, Wild1]));
    }

    [Fact]
    public void IsValid_accepts_two_different_symbols_plus_a_wildcard_as_one_of_each()
    {
        Assert.True(CardSet.IsValid([Infantry1, Cavalry1, Wild1]));
    }

    [Fact]
    public void IsValid_accepts_three_wildcards()
    {
        Assert.True(CardSet.IsValid([Wild1, Wild2, new WildCard()]));
    }

    [Fact]
    public void IsValid_rejects_a_set_that_is_not_exactly_three_cards()
    {
        Assert.False(CardSet.IsValid([Infantry1, Infantry2]));
    }
}
