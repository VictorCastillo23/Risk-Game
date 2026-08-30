using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Events;
using Risk.Engine.Modes;
using Risk.Engine.State;

namespace Risk.Tests.Modes;

public class SecretMissionSetupStrategyTests
{
    private static readonly ISetupStrategy Strategy = new SecretMissionSetupStrategy();

    [Theory]
    [InlineData(3, 35)]
    [InlineData(4, 30)]
    [InlineData(5, 25)]
    public void Create_deals_all_42_territories_equitably_and_assigns_the_starting_troop_pool(int playerCount, int startingTroops)
    {
        IReadOnlyList<PlayerId> players = Enumerable.Range(0, playerCount).Select(i => new PlayerId(i)).ToArray();

        var state = Strategy.Create(players, startingTroops);

        var counts = state.Territories.Values
            .GroupBy(t => t.Owner)
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
}
