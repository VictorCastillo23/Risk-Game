using Risk.Domain.Cards;
using Risk.Engine.Rules;

namespace Risk.Web.Models;

/// <summary>
/// Tracks which cards in the current player's hand have been toggled for a
/// prospective trade-in on <c>CardPanel</c>: click-to-toggle selection
/// capped at exactly three cards, plus a live client-side validity
/// pre-check (design's UX-hint pattern, mirroring
/// <see cref="AttackDiceOptions"/>/<see cref="OccupyBounds"/>) that reuses
/// <see cref="CardSet.IsValid"/> rather than duplicating set-validation
/// logic.
/// </summary>
/// <remarks>
/// Selection is tracked by hand index, not by <see cref="Card"/> value —
/// <see cref="WildCard"/> carries no fields, so two wildcards held in the
/// same hand are value-equal and would collapse into a single toggle
/// target if selection were keyed by the card itself.
/// </remarks>
public sealed record CardSelection(IReadOnlyList<int> SelectedIndices)
{
    private const int RequiredSetSize = 3;

    public static readonly CardSelection Empty = new(Array.Empty<int>());

    /// <summary>
    /// Deselects <paramref name="index"/> if already selected; otherwise
    /// selects it, unless <see cref="RequiredSetSize"/> cards are already
    /// selected (further clicks are a no-op until something is deselected).
    /// </summary>
    public CardSelection Toggle(int index)
    {
        if (SelectedIndices.Contains(index))
        {
            return new CardSelection(SelectedIndices.Where(i => i != index).ToArray());
        }

        if (SelectedIndices.Count >= RequiredSetSize)
        {
            return this;
        }

        return new CardSelection([.. SelectedIndices, index]);
    }

    public bool IsSelected(int index) => SelectedIndices.Contains(index);

    /// <summary>
    /// True only when exactly three cards are selected AND they form a
    /// valid trade-in set per the engine's own <see cref="CardSet.IsValid"/>
    /// rule — a client-side hint only, the engine remains authoritative.
    /// </summary>
    public bool IsValidTrade(IReadOnlyList<Card> hand) =>
        SelectedIndices.Count == RequiredSetSize && CardSet.IsValid(SelectedIndices.Select(i => hand[i]).ToArray());
}
