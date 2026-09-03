using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Views;

namespace Risk.Web.Models;

/// <summary>
/// One declared headquarters: who declared it, which territory it is, and
/// who currently holds that territory (may differ from <see cref="Declarer"/>
/// after a capture).
/// </summary>
public readonly record struct HeadquartersRow(PlayerId Declarer, TerritoryId Territory, PlayerId? Holder);

/// <summary>
/// Pure derivations over <see cref="PlayerView"/> backing the Capital-mode
/// sections of <c>CardPanel</c> and the pick-phase secrecy guarantee (design
/// D2). Kept out of any <c>.razor</c> file because this repo has no bUnit —
/// this is the only layer that can be unit-tested for these rules.
/// </summary>
public static class HeadquartersTray
{
    /// <summary>
    /// Design D1's monotonic proxy for "<c>HeadquartersRevealed</c> has
    /// fired": <see cref="PlayerView.RevealedHeadquarters"/> is built by
    /// <c>GameEngine.Observe</c> as empty until every player has picked, then
    /// non-empty forever after — and always empty outside Capital mode (no
    /// player ever has a <c>HeadquartersId</c>). <c>Count &gt; 0</c> therefore
    /// encodes "Capital AND revealed" by itself; no separate mode check is
    /// needed here, and none would be safe on its own (a mode-only gate would
    /// leak HQs during the secret pick).
    /// </summary>
    public static bool IsRevealed(PlayerView view) => view.RevealedHeadquarters.Count > 0;

    /// <summary>
    /// Every declared headquarters and its current holder, for the
    /// full-table tray. Empty before <see cref="IsRevealed"/> is true.
    /// </summary>
    public static IReadOnlyList<HeadquartersRow> Rows(PlayerView view) =>
        !IsRevealed(view)
            ? []
            : view.RevealedHeadquarters
                .OrderBy(kv => kv.Key.Value)
                .Select(kv => new HeadquartersRow(kv.Key, kv.Value, view.Territories[kv.Value].Owner))
                .ToArray();

    /// <summary>
    /// Whether <paramref name="viewer"/>'s own declared headquarters is
    /// currently owned by someone else. Purely derived from live
    /// <see cref="PlayerView.Territories"/> each call — no stored flag, so it
    /// clears for free on recapture. False before <see cref="IsRevealed"/>
    /// (structurally, no attack can target a still-secret HQ) and false when
    /// <see cref="PlayerView.OwnHeadquarters"/> is null (non-Capital modes,
    /// or Capital before the viewer has picked).
    /// </summary>
    public static bool HasLostOwnHeadquarters(PlayerView view, PlayerId viewer) =>
        IsRevealed(view) && view.OwnHeadquarters is { } hq && view.Territories[hq].Owner != viewer;
}
