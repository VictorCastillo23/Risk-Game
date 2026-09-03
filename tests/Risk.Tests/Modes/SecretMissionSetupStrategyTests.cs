using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine.Events;
using Risk.Engine.Modes;
using Risk.Engine.State;

namespace Risk.Tests.Modes;

public class SecretMissionSetupStrategyTests
{
    private static readonly ISetupStrategy Strategy = new SecretMissionSetupStrategy();

    private static readonly IReadOnlyDictionary<int, int> StartingTroopsByPlayerCount = new Dictionary<int, int>
    {
        [3] = 35,
        [4] = 30,
        [5] = 25
    };

    /// <summary>
    /// Structural equality key for a <see cref="MissionCard"/>. Records with
    /// an <c>IReadOnlyList</c> field (<see cref="ConquerContinents.Required"/>)
    /// do NOT get structural equality from the record-generated
    /// <c>Equals</c> — the list is compared by reference. This projects
    /// every mission archetype down to a comparable value so tests can use
    /// plain equality instead of a false-negative-prone raw <c>Distinct()</c>.
    /// </summary>
    private static string MissionKey(MissionCard card) => card switch
    {
        EliminateArmy(var army) => $"Eliminate:{army.Value}",
        OccupyTerritories(var count, var minArmies) => $"Occupy:{count}:{minArmies}",
        ConquerContinents(var required, var wildcards) =>
            $"Conquer:{string.Join("+", required.Select(c => c.Value))}:{wildcards}",
        _ => throw new InvalidOperationException($"Unreachable: unknown {nameof(MissionCard)} subtype.")
    };

    [Theory]
    [InlineData(3, 35)]
    [InlineData(4, 30)]
    [InlineData(5, 25)]
    public void Create_deals_all_42_territories_equitably_and_assigns_the_starting_troop_pool(int playerCount, int startingTroops)
    {
        IReadOnlyList<PlayerId> players = Enumerable.Range(0, playerCount).Select(i => new PlayerId(i)).ToArray();

        var state = Strategy.Create(players, startingTroops);

        var counts = state.Territories.Values
            .GroupBy(t => t.Owner!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(playerCount, counts.Count);
        Assert.Equal(WorldMap.Territories.Count, counts.Values.Sum());
        var min = counts.Values.Min();
        var max = counts.Values.Max();
        Assert.True(max - min <= 1, $"Territory counts should differ by at most 1; got min={min}, max={max}.");

        foreach (var player in state.Players)
        {
            var owned = counts.TryGetValue(player.Id, out var count) ? count : 0;
            Assert.Equal(startingTroops - owned, player.TroopsRemaining);
        }
    }

    [Fact]
    public void Create_starts_the_first_player_in_the_Setup_phase()
    {
        IReadOnlyList<PlayerId> players = [new PlayerId(0), new PlayerId(1), new PlayerId(2)];

        var state = Strategy.Create(players, startingTroops: 35);

        Assert.Equal(players[0], state.Turn.CurrentPlayer);
        Assert.Equal(TurnPhase.Setup, state.Turn.Phase);
    }

    [Fact]
    public void Create_stamps_SecretMission_as_the_game_mode()
    {
        IReadOnlyList<PlayerId> players = [new PlayerId(0), new PlayerId(1), new PlayerId(2)];

        var state = Strategy.Create(players, startingTroops: 35);

        Assert.Equal(GameMode.SecretMission, state.Mode);
    }

    [Fact]
    public void Create_logs_exactly_one_TerritoriesAssigned_event_covering_all_42_territories()
    {
        IReadOnlyList<PlayerId> players = [new PlayerId(0), new PlayerId(1), new PlayerId(2)];

        var state = Strategy.Create(players, startingTroops: 35);

        var assigned = Assert.Single(state.Log.OfType<TerritoriesAssigned>());
        Assert.Equal(WorldMap.Territories.Count, assigned.Assignments.Count);
    }

    [Fact]
    public void Create_stamps_a_fresh_44_card_deck()
    {
        IReadOnlyList<PlayerId> players = [new PlayerId(0), new PlayerId(1), new PlayerId(2)];

        var state = Strategy.Create(players, startingTroops: 35);

        Assert.Equal(44, state.Deck.Count);
    }

    [Theory]
    [InlineData(3, 11)]
    [InlineData(4, 12)]
    [InlineData(5, 13)]
    public void SeatedPool_excludes_EliminateArmy_cards_for_unseated_armies(int playerCount, int expectedPoolSize)
    {
        var pool = SecretMissionSetupStrategy.SeatedPool(playerCount);

        Assert.Equal(expectedPoolSize, pool.Count);
        Assert.DoesNotContain(pool, c => c is EliminateArmy(var army) && army.Value >= playerCount);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void SeatedPool_retains_EliminateArmy_for_every_seated_army(int playerCount)
    {
        var pool = SecretMissionSetupStrategy.SeatedPool(playerCount);

        for (var i = 0; i < playerCount; i++)
        {
            Assert.Contains(pool, c => c is EliminateArmy(var army) && army.Value == i);
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void SeatedPool_retains_all_OccupyTerritories_and_ConquerContinents_cards_unconditionally(int playerCount)
    {
        var pool = SecretMissionSetupStrategy.SeatedPool(playerCount);

        Assert.Equal(2, pool.OfType<OccupyTerritories>().Count());
        Assert.Equal(6, pool.OfType<ConquerContinents>().Count());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Create_deals_exactly_one_non_null_mission_per_seated_player(int playerCount)
    {
        IReadOnlyList<PlayerId> players = Enumerable.Range(0, playerCount).Select(i => new PlayerId(i)).ToArray();

        var state = Strategy.Create(players, StartingTroopsByPlayerCount[playerCount]);

        Assert.All(state.Players, p => Assert.NotNull(p.Mission));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Create_deals_distinct_missions_with_no_duplicates(int playerCount)
    {
        IReadOnlyList<PlayerId> players = Enumerable.Range(0, playerCount).Select(i => new PlayerId(i)).ToArray();

        var state = Strategy.Create(players, StartingTroopsByPlayerCount[playerCount]);

        var keys = state.Players.Select(p => MissionKey(p.Mission!)).ToArray();
        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Create_deals_only_missions_from_the_seated_pool(int playerCount)
    {
        IReadOnlyList<PlayerId> players = Enumerable.Range(0, playerCount).Select(i => new PlayerId(i)).ToArray();

        var state = Strategy.Create(players, StartingTroopsByPlayerCount[playerCount]);

        var poolKeys = SecretMissionSetupStrategy.SeatedPool(playerCount).Select(MissionKey).ToHashSet();
        Assert.All(state.Players, p => Assert.Contains(MissionKey(p.Mission!), poolKeys));
    }

    [Fact]
    public void Create_shuffles_the_dealt_mission_set_across_many_runs()
    {
        IReadOnlyList<PlayerId> players = [new PlayerId(0), new PlayerId(1), new PlayerId(2)];
        var dealtSets = new HashSet<string>();

        for (var i = 0; i < 200; i++)
        {
            var state = Strategy.Create(players, startingTroops: 35);
            var setKey = string.Join(",", state.Players.Select(p => MissionKey(p.Mission!)).OrderBy(k => k));
            dealtSets.Add(setKey);
        }

        Assert.True(dealtSets.Count >= 2, "Expected mission dealing to vary across 200 runs.");
    }
}
