using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Engine.Events;

namespace Risk.Engine.State;

/// <summary>
/// The complete, immutable state of a game: territory ownership/troops,
/// per-player state, whose turn it is, the remaining deck, an append-only
/// event log, and whether the game is still in progress.
/// </summary>
public sealed record GameState(
    IReadOnlyDictionary<TerritoryId, TerritoryState> Territories,
    IReadOnlyList<PlayerState> Players,
    TurnState Turn,
    IReadOnlyList<Card> Deck,
    IReadOnlyList<GameEvent> Log,
    GameStatus Status);
