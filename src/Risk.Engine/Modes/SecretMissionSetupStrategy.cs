using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine.Events;
using Risk.Engine.State;

namespace Risk.Engine.Modes;

/// <summary>
/// SecretMission's <see cref="ISetupStrategy"/>: deals all 42 territories
/// randomly and as equitably as possible across the given players and
/// assigns each player's official starting troop pool minus the territories
/// they were dealt (placed turn-by-turn afterwards via
/// <c>PlaceTroopsCommand</c>), then deals one secret mission card per seat
/// (reglasrisk.md setup steps 2-3). Carries exactly the pre-refactor inline
/// territory-dealing algorithm from <c>GameSetup.Create</c>. Army-to-seat
/// binding is positional: <see cref="ArmyId"/>(i) is seat <see cref="PlayerId"/>(i),
/// matching the existing <c>ownedCounts[owner.Value]</c> convention below.
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

        var missions = DealMissions(playerCount);

        var playerStates = players
            .Select((p, i) => new PlayerState(p, [], false, startingTroops - ownedCounts[i], Mission: missions[i]))
            .ToArray();

        var turn = new TurnState(players[0], TurnPhase.Setup);
        IReadOnlyDictionary<TerritoryId, PlayerId> assignments = territories.ToDictionary(kv => kv.Key, kv => kv.Value.Owner!.Value);
        var events = new List<GameEvent> { new TerritoriesAssigned(assignments) };

        return new GameState(territories, playerStates, turn, Deck.CreateStandard(), events,
            new GameStatus.InProgress(), Mode: GameMode.SecretMission);
    }

    /// <summary>
    /// Setup step 2 (reglasrisk.md:109): armies ArmyId(playerCount)..ArmyId(5)
    /// are unseated, so their EliminateArmy cards go back in the box before
    /// shuffling. Positional binding: ArmyId(i) is seat PlayerId(i).
    /// A card naming the holder's OWN army is a legal deal outcome and is dealt
    /// as-is — its printed fallback (reglasrisk.md:84-85) is resolved at
    /// completion-check time (roadmap 3.3), never substituted here.
    /// </summary>
    internal static IReadOnlyList<MissionCard> SeatedPool(int playerCount) =>
        MissionDeck.CreateStandard()
            .Where(c => c is not EliminateArmy(var army) || army.Value < playerCount)
            .ToArray();

    private static IReadOnlyList<MissionCard> DealMissions(int playerCount) =>
        SeatedPool(playerCount)
            .OrderBy(_ => Random.Shared.Next())
            .Take(playerCount)
            .ToArray();
}
