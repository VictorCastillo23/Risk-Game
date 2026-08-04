using Risk.Domain.Map;

namespace Risk.Domain.Cards;

/// <summary>
/// Builds the standard 44-card Risk deck: 14 infantry, 14 cavalry,
/// 14 artillery territory cards, plus 2 wildcards.
/// </summary>
/// <remarks>
/// The exact classic per-territory symbol assignment is an open design
/// question (see design.md); the real 42 territory names are seeded later
/// by <c>WorldMap</c> (PR3). Until then, territory cards are keyed by
/// placeholder identifiers so this deck can be built and tested by symbol
/// distribution alone, independent of the board data.
/// </remarks>
public static class Deck
{
    private const int TerritoriesPerSymbol = 14;
    private const int WildcardCount = 2;

    public static IReadOnlyList<Card> CreateStandard()
    {
        var cards = new List<Card>();
        var territoryNumber = 1;

        foreach (var symbol in new[] { CardSymbol.Infantry, CardSymbol.Cavalry, CardSymbol.Artillery })
        {
            for (var i = 0; i < TerritoriesPerSymbol; i++)
            {
                var territoryId = new TerritoryId($"Territory{territoryNumber:D2}");
                cards.Add(new TerritoryCard(territoryId, symbol));
                territoryNumber++;
            }
        }

        for (var i = 0; i < WildcardCount; i++)
        {
            cards.Add(new WildCard());
        }

        return cards;
    }
}
