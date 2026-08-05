using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class CardSelectionTests
{
    [Fact]
    public void Toggle_UnselectedIndex_SelectsIt()
    {
        var selection = CardSelection.Empty.Toggle(0);

        Assert.True(selection.IsSelected(0));
    }

    [Fact]
    public void Toggle_AlreadySelectedIndex_DeselectsIt()
    {
        var selection = CardSelection.Empty.Toggle(0).Toggle(0);

        Assert.False(selection.IsSelected(0));
    }

    [Fact]
    public void Toggle_FourthIndex_IsIgnoredWhileThreeAlreadySelected()
    {
        var selection = CardSelection.Empty.Toggle(0).Toggle(1).Toggle(2).Toggle(3);

        Assert.True(selection.IsSelected(0));
        Assert.True(selection.IsSelected(1));
        Assert.True(selection.IsSelected(2));
        Assert.False(selection.IsSelected(3));
    }

    [Fact]
    public void IsValidTrade_FewerThanThreeSelected_ReturnsFalse()
    {
        var hand = new Card[]
        {
            new TerritoryCard(new TerritoryId("alaska"), CardSymbol.Infantry),
            new TerritoryCard(new TerritoryId("iceland"), CardSymbol.Cavalry),
        };

        var selection = CardSelection.Empty.Toggle(0).Toggle(1);

        Assert.False(selection.IsValidTrade(hand));
    }

    [Fact]
    public void IsValidTrade_ThreeCardsOneOfEachSymbol_ReturnsTrue()
    {
        var hand = new Card[]
        {
            new TerritoryCard(new TerritoryId("alaska"), CardSymbol.Infantry),
            new TerritoryCard(new TerritoryId("iceland"), CardSymbol.Cavalry),
            new TerritoryCard(new TerritoryId("japan"), CardSymbol.Artillery),
        };

        var selection = CardSelection.Empty.Toggle(0).Toggle(1).Toggle(2);

        Assert.True(selection.IsValidTrade(hand));
    }

    [Fact]
    public void IsValidTrade_ThreeCardsTwoMatchingOneDifferent_ReturnsFalse()
    {
        var hand = new Card[]
        {
            new TerritoryCard(new TerritoryId("alaska"), CardSymbol.Infantry),
            new TerritoryCard(new TerritoryId("iceland"), CardSymbol.Infantry),
            new TerritoryCard(new TerritoryId("japan"), CardSymbol.Artillery),
        };

        var selection = CardSelection.Empty.Toggle(0).Toggle(1).Toggle(2);

        Assert.False(selection.IsValidTrade(hand));
    }

    [Fact]
    public void IsValidTrade_ThreeOfAKindPlusWildCard_ReturnsTrue()
    {
        var hand = new Card[]
        {
            new TerritoryCard(new TerritoryId("alaska"), CardSymbol.Infantry),
            new TerritoryCard(new TerritoryId("iceland"), CardSymbol.Infantry),
            new WildCard(),
        };

        var selection = CardSelection.Empty.Toggle(0).Toggle(1).Toggle(2);

        Assert.True(selection.IsValidTrade(hand));
    }

    [Fact]
    public void Toggle_TwoValueEqualWildCardsAtDifferentIndices_BothSelectableIndependently()
    {
        // Regression guard: selection must be tracked by index, not by Card
        // value equality — WildCard has no fields, so two wildcards held in
        // the same hand are value-equal and would collapse into a single
        // toggle target if selection were keyed by Card instead of index.
        var selection = CardSelection.Empty.Toggle(0).Toggle(1);

        Assert.True(selection.IsSelected(0));
        Assert.True(selection.IsSelected(1));
    }
}
