using Risk.Domain.Map;

namespace Risk.Domain.Cards;

/// <summary>
/// Builds the standard 44-card Risk deck: one territory card per real
/// <see cref="WorldMap"/> territory (14 infantry, 14 cavalry, 14 artillery,
/// per WorldMap's even symbol assignment), plus 2 wildcards.
/// </summary>
public static class Deck
{
    private const int WildcardCount = 2;

    public static IReadOnlyList<Card> CreateStandard()
    {
        var cards = new List<Card>(WorldMap.Territories.Count + WildcardCount);

        foreach (var territory in WorldMap.Territories)
        {
            cards.Add(new TerritoryCard(territory.Id, territory.Symbol));
        }

        for (var i = 0; i < WildcardCount; i++)
        {
            cards.Add(new WildCard());
        }

        return cards;
    }
}
