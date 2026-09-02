using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Engine.Views;

/// <summary>
/// A player's redacted view of the game: their own hand in full, other
/// players' hands reduced to a card count, and the public board/turn state.
/// Prevents any client from seeing hidden information belonging to others.
/// </summary>
/// <param name="OwnHeadquarters">
/// The viewer's own headquarters territory, visible to them from the moment
/// they select it (Capital-mode <see cref="Risk.Engine.State.TurnPhase.SelectHeadquarters"/>).
/// Always <see langword="null"/> outside Capital mode.
/// </param>
/// <param name="RevealedHeadquarters">
/// Every player's headquarters territory, including the viewer's own, once
/// every player has selected one (design D1: derived from
/// <c>Players.All(p =&gt; p.HeadquartersId is not null)</c>, never un-reveals).
/// Empty before that point and always empty outside Capital mode.
/// </param>
public sealed record PlayerView(
    IReadOnlyDictionary<TerritoryId, TerritoryState> Territories,
    IReadOnlyList<Card> OwnHand,
    IReadOnlyDictionary<PlayerId, int> OtherPlayersCardCounts,
    TurnState Turn,
    TerritoryId? OwnHeadquarters,
    IReadOnlyDictionary<PlayerId, TerritoryId> RevealedHeadquarters);
