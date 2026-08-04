using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Engine.Events;

namespace Risk.Engine.State;

/// <summary>
/// The complete, immutable state of a game: territory ownership/troops,
/// per-player state, whose turn it is, the remaining deck, an append-only
/// event log, whether the game is still in progress, and how many card
/// sets have been traded in so far this game (drives the escalating
/// trade-in bonus scale).
/// </summary>
public sealed record GameState(
    IReadOnlyDictionary<TerritoryId, TerritoryState> Territories,
    IReadOnlyList<PlayerState> Players,
    TurnState Turn,
    IReadOnlyList<Card> Deck,
    IReadOnlyList<GameEvent> Log,
    GameStatus Status,
    int TradesCompleted = 0);
