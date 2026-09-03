using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine.Modes;
using Risk.Engine.State;

namespace Risk.Tests.Modes;

public class SecretMissionVictoryRuleTests
{
    private static readonly IVictoryRule Rule = new SecretMissionVictoryRule();

    private static readonly PlayerId PlayerA = new(0);
    private static readonly PlayerId PlayerB = new(1);
    private static readonly PlayerId PlayerC = new(2);

    // ---- OccupyTerritories ----

    [Fact]
    public void CheckVictory_reports_the_winner_at_the_exact_Count_and_MinArmiesPerTerritory_boundary()
    {
        var mission = new OccupyTerritories(Count: 5, MinArmiesPerTerritory: 3);
        var territories = BoardWithOwnedCount(PlayerA, ownedCount: 5, ownedTroops: 3, filler: PlayerB);
        var state = BuildState(
            territories,
            [
                new PlayerState(PlayerA, [], false, 0, Mission: mission),
                new PlayerState(PlayerB, [], false, 0)
            ]);

        Assert.Equal(PlayerA, Rule.CheckVictory(state));
    }

    [Fact]
    public void CheckVictory_returns_null_when_one_territory_short_of_OccupyTerritories_Count()
    {
        var mission = new OccupyTerritories(Count: 5, MinArmiesPerTerritory: 3);
        var territories = BoardWithOwnedCount(PlayerA, ownedCount: 4, ownedTroops: 3, filler: PlayerB);
        var state = BuildState(
            territories,
            [
                new PlayerState(PlayerA, [], false, 0, Mission: mission),
                new PlayerState(PlayerB, [], false, 0)
            ]);

        Assert.Null(Rule.CheckVictory(state));
    }

    // ---- EliminateArmy ----

    [Fact]
    public void CheckVictory_reports_the_winner_when_the_targeted_army_is_eliminated()
    {
        var mission = new EliminateArmy(new ArmyId(PlayerB.Value));
        var territories = UniformBoard(PlayerC);
        var state = BuildState(
            territories,
            [
                new PlayerState(PlayerA, [], false, 0, Mission: mission),
                new PlayerState(PlayerB, [], true, 0),
                new PlayerState(PlayerC, [], false, 0)
            ]);

        Assert.Equal(PlayerA, Rule.CheckVictory(state));
    }

    [Fact]
    public void CheckVictory_returns_null_when_the_targeted_army_is_still_active()
    {
        var mission = new EliminateArmy(new ArmyId(PlayerB.Value));
        var territories = UniformBoard(PlayerC);
        var state = BuildState(
            territories,
            [
                new PlayerState(PlayerA, [], false, 0, Mission: mission),
                new PlayerState(PlayerB, [], false, 0),
                new PlayerState(PlayerC, [], false, 0)
            ]);

        Assert.Null(Rule.CheckVictory(state));
    }

    // ---- ConquerContinents ----

    [Fact]
    public void CheckVictory_reports_the_winner_when_Required_continents_and_wildcards_are_fully_owned()
    {
        var mission = new ConquerContinents(Required: [new ContinentId("NA")], WildcardCount: 1);
        var territories = BoardWithContinentsOwnedBy(
            PlayerA,
            fullyOwned: [new ContinentId("NA"), new ContinentId("AF")],
            filler: PlayerB);
        var state = BuildState(
            territories,
            [
                new PlayerState(PlayerA, [], false, 0, Mission: mission),
                new PlayerState(PlayerB, [], false, 0)
            ]);

        Assert.Equal(PlayerA, Rule.CheckVictory(state));
    }

    [Fact]
    public void CheckVictory_returns_null_when_a_Required_continent_is_missing()
    {
        var mission = new ConquerContinents(Required: [new ContinentId("NA"), new ContinentId("EU")], WildcardCount: 0);
        var territories = BoardWithContinentsOwnedBy(
            PlayerA,
            fullyOwned: [new ContinentId("NA"), new ContinentId("AF"), new ContinentId("AS"), new ContinentId("OC")],
            filler: PlayerB);
        var state = BuildState(
            territories,
            [
                new PlayerState(PlayerA, [], false, 0, Mission: mission),
                new PlayerState(PlayerB, [], false, 0)
            ]);

        Assert.Null(Rule.CheckVictory(state));
    }

    [Fact]
    public void CheckVictory_returns_null_when_Required_is_complete_but_wildcards_are_short()
    {
        var mission = new ConquerContinents(Required: [new ContinentId("NA")], WildcardCount: 2);
        var territories = BoardWithContinentsOwnedBy(
            PlayerA,
            fullyOwned: [new ContinentId("NA"), new ContinentId("AF")],
            filler: PlayerB);
        var state = BuildState(
            territories,
            [
                new PlayerState(PlayerA, [], false, 0, Mission: mission),
                new PlayerState(PlayerB, [], false, 0)
            ]);

        Assert.Null(Rule.CheckVictory(state));
    }

    // ---- Own-army substitution (design D4) ----

    [Fact]
    public void CheckVictory_evaluates_a_self_targeting_EliminateArmy_as_OccupyTerritories_24_1_and_wins()
    {
        var selfMission = new EliminateArmy(new ArmyId(PlayerA.Value));
        var territories = BoardWithOwnedCount(PlayerA, ownedCount: 24, ownedTroops: 1, filler: PlayerB);
        var state = BuildState(
            territories,
            [
                new PlayerState(PlayerA, [], false, 0, Mission: selfMission),
                new PlayerState(PlayerB, [], false, 0)
            ]);

        Assert.Equal(PlayerA, Rule.CheckVictory(state));
        Assert.Equal(selfMission, state.Players[0].Mission);
    }

    [Fact]
    public void CheckVictory_returns_null_for_a_self_targeting_EliminateArmy_below_the_substituted_threshold()
    {
        var selfMission = new EliminateArmy(new ArmyId(PlayerA.Value));
        var territories = BoardWithOwnedCount(PlayerA, ownedCount: 23, ownedTroops: 1, filler: PlayerB);
        var state = BuildState(
            territories,
            [
                new PlayerState(PlayerA, [], false, 0, Mission: selfMission),
                new PlayerState(PlayerB, [], false, 0)
            ]);

        Assert.Null(Rule.CheckVictory(state));
    }

    // ---- Structural cases ----

    [Fact]
    public void CheckVictory_reports_a_non_actor_player_whose_mission_is_complete()
    {
        var mission = new OccupyTerritories(Count: 5, MinArmiesPerTerritory: 1);
        var territories = BoardWithOwnedCount(PlayerB, ownedCount: 5, ownedTroops: 1, filler: PlayerA);
        var state = BuildState(
            territories,
            [
                new PlayerState(PlayerA, [], false, 0),
                new PlayerState(PlayerB, [], false, 0, Mission: mission)
            ],
            currentPlayer: PlayerA);

        Assert.Equal(PlayerB, Rule.CheckVictory(state));
    }

    [Fact]
    public void CheckVictory_never_reports_an_eliminated_player_even_with_a_complete_mission()
    {
        var mission = new OccupyTerritories(Count: 5, MinArmiesPerTerritory: 1);
        var territories = BoardWithOwnedCount(PlayerA, ownedCount: 5, ownedTroops: 1, filler: PlayerB);
        var state = BuildState(
            territories,
            [
                new PlayerState(PlayerA, [], true, 0, Mission: mission),
                new PlayerState(PlayerB, [], false, 0)
            ]);

        Assert.Null(Rule.CheckVictory(state));
    }

    [Fact]
    public void CheckVictory_never_reports_a_player_with_a_null_Mission()
    {
        var territories = BoardWithOwnedCount(PlayerA, ownedCount: 42, ownedTroops: 5, filler: PlayerB);
        var state = BuildState(
            territories,
            [
                new PlayerState(PlayerA, [], false, 0, Mission: null),
                new PlayerState(PlayerB, [], false, 0)
            ]);

        Assert.Null(Rule.CheckVictory(state));
    }

    // ---- Fixtures ----

    private static Dictionary<TerritoryId, TerritoryState> BoardWithOwnedCount(
        PlayerId owner, int ownedCount, int ownedTroops, PlayerId filler)
    {
        var all = WorldMap.Territories;
        var territories = new Dictionary<TerritoryId, TerritoryState>();
        for (var i = 0; i < all.Count; i++)
        {
            territories[all[i].Id] = i < ownedCount
                ? new TerritoryState(owner, ownedTroops)
                : new TerritoryState(filler, 1);
        }

        return territories;
    }

    private static Dictionary<TerritoryId, TerritoryState> UniformBoard(PlayerId owner) =>
        WorldMap.Territories.ToDictionary(t => t.Id, t => new TerritoryState(owner, 1));

    private static Dictionary<TerritoryId, TerritoryState> BoardWithContinentsOwnedBy(
        PlayerId owner, IReadOnlyList<ContinentId> fullyOwned, PlayerId filler)
    {
        var ownedSet = fullyOwned.ToHashSet();
        var territories = new Dictionary<TerritoryId, TerritoryState>();
        foreach (var continent in Continents.All)
        {
            var isOwned = ownedSet.Contains(continent.Id);
            foreach (var member in continent.Members)
            {
                territories[member] = new TerritoryState(isOwned ? owner : filler, 1);
            }
        }

        return territories;
    }

    private static GameState BuildState(
        IReadOnlyDictionary<TerritoryId, TerritoryState> territories,
        IReadOnlyList<PlayerState> players,
        PlayerId? currentPlayer = null)
    {
        var turn = new TurnState(currentPlayer ?? players[0].Id, TurnPhase.Attack);

        return new GameState(
            territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress(), Mode: GameMode.SecretMission);
    }
}
