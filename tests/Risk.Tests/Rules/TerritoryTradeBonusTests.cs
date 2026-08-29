using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Rules;
using Risk.Engine.State;

namespace Risk.Tests.Rules;

public class TerritoryTradeBonusTests
{
    private static readonly PlayerId Actor = new(0);
    private static readonly PlayerId Other = new(1);

    [Fact]
    public void Troops_constant_is_two()
    {
        Assert.Equal(2, TerritoryTradeBonus.Troops);
    }

    [Fact]
    public void ResolveMatches_returns_empty_when_no_traded_card_names_an_owned_territory()
    {
        var territories = BuildTerritories(
            (new TerritoryId("Alaska"), Other),
            (new TerritoryId("Alberta"), Other));

        IReadOnlyList<Card> cards =
        [
            new TerritoryCard(new TerritoryId("Alaska"), CardSymbol.Infantry),
            new TerritoryCard(new TerritoryId("Alberta"), CardSymbol.Infantry),
            new WildCard()
        ];

        var matches = TerritoryTradeBonus.ResolveMatches(cards, territories, Actor);

        Assert.Empty(matches);
    }

    [Fact]
    public void ResolveMatches_returns_the_single_owned_territory_named_by_a_traded_card()
    {
        var territories = BuildTerritories(
            (new TerritoryId("Alaska"), Actor),
            (new TerritoryId("Alberta"), Other));

        IReadOnlyList<Card> cards =
        [
            new TerritoryCard(new TerritoryId("Alaska"), CardSymbol.Infantry),
            new TerritoryCard(new TerritoryId("Alberta"), CardSymbol.Infantry),
            new WildCard()
        ];

        var matches = TerritoryTradeBonus.ResolveMatches(cards, territories, Actor);

        var match = Assert.Single(matches);
        Assert.Equal(new TerritoryId("Alaska"), match);
    }

    [Fact]
    public void ResolveMatches_returns_every_distinct_owned_territory_named_by_traded_cards()
    {
        var territories = BuildTerritories(
            (new TerritoryId("Alaska"), Actor),
            (new TerritoryId("Alberta"), Actor),
            (new TerritoryId("Ontario"), Other));

        IReadOnlyList<Card> cards =
        [
            new TerritoryCard(new TerritoryId("Alaska"), CardSymbol.Infantry),
            new TerritoryCard(new TerritoryId("Alberta"), CardSymbol.Infantry),
            new TerritoryCard(new TerritoryId("Ontario"), CardSymbol.Infantry)
        ];

        var matches = TerritoryTradeBonus.ResolveMatches(cards, territories, Actor);

        Assert.Equal(2, matches.Count);
        Assert.Contains(new TerritoryId("Alaska"), matches);
        Assert.Contains(new TerritoryId("Alberta"), matches);
    }

    [Fact]
    public void ResolveMatches_never_counts_wildcards_even_though_they_carry_no_territory()
    {
        var territories = BuildTerritories((new TerritoryId("Alaska"), Actor));

        IReadOnlyList<Card> cards =
        [
            new WildCard(),
            new WildCard(),
            new TerritoryCard(new TerritoryId("Alberta"), CardSymbol.Infantry)
        ];

        var matches = TerritoryTradeBonus.ResolveMatches(cards, territories, Actor);

        Assert.Empty(matches);
    }

    [Fact]
    public void ResolveMatches_counts_a_territory_named_by_two_traded_cards_only_once()
    {
        // Not achievable with real TerritoryCards holding one Territory each in
        // practice (a hand can't hold two cards for the same territory under
        // the standard deck), but the rule must still de-duplicate defensively
        // since CardSet.IsValid does not forbid it for WildCard-free triples
        // constructed directly in a test.
        var territories = BuildTerritories((new TerritoryId("Alaska"), Actor));

        IReadOnlyList<Card> cards =
        [
            new TerritoryCard(new TerritoryId("Alaska"), CardSymbol.Infantry),
            new TerritoryCard(new TerritoryId("Alaska"), CardSymbol.Cavalry),
            new WildCard()
        ];

        var matches = TerritoryTradeBonus.ResolveMatches(cards, territories, Actor);

        Assert.Single(matches);
    }

    private static IReadOnlyDictionary<TerritoryId, TerritoryState> BuildTerritories(
        params (TerritoryId Id, PlayerId Owner)[] entries) =>
        entries.ToDictionary(e => e.Id, e => new TerritoryState(e.Owner, 1));
}
