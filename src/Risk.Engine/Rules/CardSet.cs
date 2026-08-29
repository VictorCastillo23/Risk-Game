using Risk.Domain.Cards;

namespace Risk.Engine.Rules;

/// <summary>
/// Validates whether a hand of cards forms a legal trade-in set: three of
/// the same symbol, one of each symbol, or two of the same symbol plus one
/// wildcard.
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
        var wildcardCount = cards.Count - nonWildSymbols.Count;
        var distinctSymbols = nonWildSymbols.Distinct().Count();

        return wildcardCount switch
        {
            0 => distinctSymbols == 1 || distinctSymbols == nonWildSymbols.Count,
            1 => distinctSymbols == 1,
            _ => false,
        };
    }
}
