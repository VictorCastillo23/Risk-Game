using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Rules;
using Risk.Engine.Views;

namespace Risk.AI.Scoring;

/// <summary>
/// Finds the first valid 3-card trade-in set in a player's hand (per
/// <see cref="CardSet.IsValid"/>), enumerating 3-card combinations in hand
/// order for a deterministic pick, and resolves the occupied-territory bonus
/// target via <see cref="TerritoryTradeBonus"/>.
/// </summary>
internal static class CardTradeSelection
{
    private const int SetSize = 3;

    /// <summary>
    /// The best valid <see cref="TradeCardsCommand"/> for <paramref name="self"/>'s
    /// current hand, or <see langword="null"/> if no 3-card subset satisfies
    /// <see cref="CardSet.IsValid"/> (e.g. a hand whose only combinations use
    /// two wildcards at once).
    /// </summary>
    public static TradeCardsCommand? BestTrade(PlayerView view, PlayerId self)
    {
        foreach (var combination in Combinations(view.OwnHand, SetSize))
        {
            if (!CardSet.IsValid(combination))
            {
                continue;
            }

            var matches = TerritoryTradeBonus.ResolveMatches(combination, view.Territories, self);
            var bonusTerritory = matches.Count > 0 ? matches[0] : (TerritoryId?)null;

            return new TradeCardsCommand(self, combination, bonusTerritory);
        }

        return null;
    }

    private static IEnumerable<IReadOnlyList<Card>> Combinations(IReadOnlyList<Card> hand, int size)
    {
        if (hand.Count < size)
        {
            yield break;
        }

        var indices = new int[size];
        for (var i = 0; i < size; i++)
        {
            indices[i] = i;
        }

        while (true)
        {
            yield return indices.Select(i => hand[i]).ToArray();

            var slot = size - 1;
            while (slot >= 0 && indices[slot] == hand.Count - size + slot)
            {
                slot--;
            }

            if (slot < 0)
            {
                yield break;
            }

            indices[slot]++;
            for (var i = slot + 1; i < size; i++)
            {
                indices[i] = indices[i - 1] + 1;
            }
        }
    }
}
