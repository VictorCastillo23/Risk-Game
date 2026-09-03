using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Engine.Rules;

/// <summary>
/// The classic Risk occupied-territory trade-in bonus: a flat +2 troops,
/// placed directly on one territory named by a traded card that the
/// trading player currently owns (pinned in the proposal).
/// </summary>
public static class TerritoryTradeBonus
{
    public const int Troops = 2;

    /// <summary>
    /// Every distinct territory named by <paramref name="cards"/> that
    /// <paramref name="actor"/> currently owns, per <paramref name="territories"/>.
    /// <see cref="WildCard"/>s carry no territory and never contribute a match.
    /// </summary>
    public static IReadOnlyList<TerritoryId> ResolveMatches(
        IReadOnlyList<Card> cards,
        IReadOnlyDictionary<TerritoryId, TerritoryState> territories,
        PlayerId actor) =>
        cards
            .OfType<TerritoryCard>()
            .Select(c => c.Territory)
            .Distinct()
            .Where(t => territories.TryGetValue(t, out var state) && state.Owner == actor)
            .ToArray();
}
