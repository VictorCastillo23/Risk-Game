using Risk.Domain.Cards;

namespace Risk.Engine.Rules;

/// <summary>
/// Validates whether a hand of cards forms a legal trade-in set: three of
/// the same symbol, one of each symbol, or any combination using wildcards
/// to stand in for a missing symbol.
/// </summary>
public static class CardSet
{
    private const int SetSize = 3;

    public static bool IsValid(IReadOnlyList<Card> cards)
    {
        if (cards.Count != SetSize)
        {
            return false;
        }

        var nonWildSymbols = cards.OfType<TerritoryCard>().Select(c => c.Symbol).ToList();
        var distinctSymbols = nonWildSymbols.Distinct().Count();

        var isThreeOfAKind = distinctSymbols <= 1;
        var isOneOfEach = distinctSymbols == nonWildSymbols.Count;

        return isThreeOfAKind || isOneOfEach;
    }
}
