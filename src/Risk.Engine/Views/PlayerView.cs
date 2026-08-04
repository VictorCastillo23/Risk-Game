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
public sealed record PlayerView(
    IReadOnlyDictionary<TerritoryId, TerritoryState> Territories,
    IReadOnlyList<Card> OwnHand,
    IReadOnlyDictionary<PlayerId, int> OtherPlayersCardCounts,
    TurnState Turn);
