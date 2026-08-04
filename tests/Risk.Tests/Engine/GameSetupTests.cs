using Risk.Domain.Errors;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Setup;
using Risk.Engine.State;

namespace Risk.Tests.Engine;

public class GameSetupTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void Create_rejects_invalid_player_counts(int playerCount)
    {
        var result = GameSetup.Create(playerCount);

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidPlayerCount, rejection.Error.Code);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void Create_accepts_valid_player_counts(int playerCount)
    {
        var result = GameSetup.Create(playerCount);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
    }

    [Fact]
    public void Create_deals_all_42_territories_equitably_across_4_players()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(GameSetup.Create(4));

        var counts = ok.State.Territories.Values
            .GroupBy(t => t.Owner)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(4, counts.Count);
        Assert.Equal(42, counts.Values.Sum());
        Assert.All(counts.Values, count => Assert.InRange(count, 10, 11));
    }

    [Theory]
    [InlineData(2, 40)]
    [InlineData(3, 35)]
    [InlineData(4, 30)]
    [InlineData(5, 25)]
    [InlineData(6, 20)]
    public void Create_assigns_the_official_starting_troop_pool(int playerCount, int startingTroops)
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(GameSetup.Create(playerCount));

        var totalRemaining = ok.State.Players.Sum(p => p.TroopsRemaining);
        var territoriesPlaced = ok.State.Territories.Count; // 1 troop auto-placed per dealt territory

        Assert.Equal(playerCount * startingTroops, totalRemaining + territoriesPlaced);
    }

    [Fact]
    public void Turn_based_placement_ends_only_when_all_players_reach_zero_remaining_troops()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(GameSetup.Create(2));
        var state = ok.State;
        var engine = new Risk.Engine.GameEngine();

        while (state.Players.Any(p => p.TroopsRemaining > 0))
        {
            var actor = state.Turn.CurrentPlayer;
            var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;

            var result = engine.Execute(state, new PlaceTroopsCommand(actor, territory, 1));
            var accepted = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
            state = accepted.State;
        }

        Assert.All(state.Players, p => Assert.Equal(0, p.TroopsRemaining));
        Assert.Equal(TurnPhase.Reinforce, state.Turn.Phase);
        Assert.Equal(new PlayerId(0), state.Turn.CurrentPlayer);
    }
}
