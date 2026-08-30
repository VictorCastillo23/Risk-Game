using Risk.Domain.Cards;
using Risk.Domain.Errors;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;

namespace Risk.Engine.Setup;

/// <summary>
/// Builds the very first <see cref="GameState"/> for a new game: validates
/// the player count, deals all 42 territories randomly and as equitably as
/// possible, and gives every player their official starting troop pool
/// (placed turn-by-turn afterwards via <c>PlaceTroopsCommand</c>).
/// </summary>
public static class GameSetup
{
    private static readonly IReadOnlyDictionary<int, int> StartingTroopsByPlayerCount = new Dictionary<int, int>
    {
        [2] = 40,
        [3] = 35,
        [4] = 30,
        [5] = 25
    };

    /// <summary>
    /// Every mode's legal player-count range. <c>TwoPlayer</c> is exactly 2
    /// (its neutral third army is not a real player); every other mode is
    /// 3-5. No mode accepts 6. Deliberately has no <c>_</c> discard arm: an
    /// unhandled <see cref="GameMode"/> value throws <see cref="SwitchExpressionException"/>
    /// (a programmer error, per this repo's convention) instead of silently
    /// returning a wrong range, and adding a 5th mode without updating this
    /// switch produces a CS8509 exhaustiveness warning at compile time.
    /// </summary>
    private static (int Min, int Max) PlayerCountRange(GameMode mode) => mode switch
    {
        GameMode.TwoPlayer => (2, 2),
        GameMode.Classic or GameMode.SecretMission or GameMode.Capital => (3, 5)
    };

    public static CommandResult<GameState, GameEvent> Create(int playerCount, GameMode mode)
    {
        var (min, max) = PlayerCountRange(mode);

        if (playerCount < min || playerCount > max)
        {
            var message = min == max
                ? $"{mode} mode requires exactly {min} players."
                : $"{mode} mode requires between {min} and {max} players.";

            return new CommandResult<GameState, GameEvent>.Rejected(
                new GameError(GameErrorCode.InvalidPlayerCount, message));
        }

        var players = Enumerable.Range(0, playerCount).Select(i => new PlayerId(i)).ToArray();
        var shuffledTerritoryIds = WorldMap.Territories
            .Select(t => t.Id)
            .OrderBy(_ => Random.Shared.Next())
            .ToArray();

        var territories = new Dictionary<TerritoryId, TerritoryState>();
        var ownedCounts = new int[playerCount];

        for (var i = 0; i < shuffledTerritoryIds.Length; i++)
        {
            var owner = players[i % playerCount];
            territories[shuffledTerritoryIds[i]] = new TerritoryState(owner, 1);
            ownedCounts[owner.Value]++;
        }

        var startingTroops = StartingTroopsByPlayerCount[playerCount];
        var playerStates = players
            .Select((p, i) => new PlayerState(p, [], false, startingTroops - ownedCounts[i]))
            .ToArray();

        var turn = new TurnState(players[0], TurnPhase.Setup);
        IReadOnlyDictionary<TerritoryId, PlayerId> assignments = territories.ToDictionary(kv => kv.Key, kv => kv.Value.Owner);
        var events = new List<GameEvent> { new TerritoriesAssigned(assignments) };

        var state = new GameState(territories, playerStates, turn, Deck.CreateStandard(), events,
            new GameStatus.InProgress(), Mode: mode);

        return new CommandResult<GameState, GameEvent>.Ok(state, events);
    }
}
