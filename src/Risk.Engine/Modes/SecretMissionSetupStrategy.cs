using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Events;
using Risk.Engine.State;

namespace Risk.Engine.Modes;

/// <summary>
/// SecretMission's <see cref="ISetupStrategy"/>: deals all 42 territories
/// randomly and as equitably as possible across the given players and
/// assigns each player's official starting troop pool minus the territories
/// they were dealt (placed turn-by-turn afterwards via
/// <c>PlaceTroopsCommand</c>). Carries exactly the pre-refactor inline
/// algorithm from <c>GameSetup.Create</c>.
/// </summary>
public sealed class SecretMissionSetupStrategy : ISetupStrategy
{
    public GameState Create(IReadOnlyList<PlayerId> players, int startingTroops)
    {
        var playerCount = players.Count;
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

        var playerStates = players
            .Select((p, i) => new PlayerState(p, [], false, startingTroops - ownedCounts[i]))
            .ToArray();

        var turn = new TurnState(players[0], TurnPhase.Setup);
        IReadOnlyDictionary<TerritoryId, PlayerId> assignments = territories.ToDictionary(kv => kv.Key, kv => kv.Value.Owner);
        var events = new List<GameEvent> { new TerritoriesAssigned(assignments) };

        return new GameState(territories, playerStates, turn, Deck.CreateStandard(), events,
            new GameStatus.InProgress(), Mode: GameMode.SecretMission);
    }
}
