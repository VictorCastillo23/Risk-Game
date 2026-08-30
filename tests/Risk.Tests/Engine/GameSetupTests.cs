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
    public static IEnumerable<object[]> AllModes() =>
        Enum.GetValues<GameMode>().Select(mode => new object[] { mode });

    [Fact]
    public void Create_rejects_two_players_outside_TwoPlayer_mode()
    {
        var result = GameSetup.Create(2, GameMode.Classic);

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidPlayerCount, rejection.Error.Code);
    }

    [Theory]
    [MemberData(nameof(AllModes))]
    public void Create_rejects_six_players_in_every_mode(GameMode mode)
    {
        var result = GameSetup.Create(6, mode);

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidPlayerCount, rejection.Error.Code);
    }

    [Theory]
    [InlineData(GameMode.TwoPlayer, 2)]
    [InlineData(GameMode.SecretMission, 3)]
    [InlineData(GameMode.SecretMission, 4)]
    [InlineData(GameMode.SecretMission, 5)]
    [InlineData(GameMode.Classic, 3)]
    [InlineData(GameMode.Classic, 4)]
    [InlineData(GameMode.Classic, 5)]
    [InlineData(GameMode.Capital, 3)]
    [InlineData(GameMode.Capital, 4)]
    [InlineData(GameMode.Capital, 5)]
    public void Create_accepts_only_the_legal_player_counts_for_each_mode(GameMode mode, int playerCount)
    {
        var result = GameSetup.Create(playerCount, mode);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
    }

    [Theory]
    [InlineData(GameMode.TwoPlayer, 1)]
    [InlineData(GameMode.TwoPlayer, 3)]
    [InlineData(GameMode.Classic, 1)]
    [InlineData(GameMode.Classic, 2)]
    [InlineData(GameMode.Classic, 7)]
    [InlineData(GameMode.SecretMission, 2)]
    [InlineData(GameMode.Capital, 2)]
    public void Create_rejects_illegal_player_counts_for_each_mode(GameMode mode, int playerCount)
    {
        var result = GameSetup.Create(playerCount, mode);

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidPlayerCount, rejection.Error.Code);
    }

    [Theory]
    [InlineData(GameMode.Classic, 2)]
    [InlineData(GameMode.TwoPlayer, 3)]
    public void Create_names_the_mode_in_the_rejection_message(GameMode mode, int playerCount)
    {
        var result = GameSetup.Create(playerCount, mode);

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Contains(mode.ToString(), rejection.Error.Message);
    }

    [Fact]
    public void Create_sets_the_mode_on_the_resulting_state()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(GameSetup.Create(2, GameMode.TwoPlayer));

        Assert.Equal(GameMode.TwoPlayer, ok.State.Mode);
    }

    [Fact]
    public void Create_marks_no_player_as_neutral()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(GameSetup.Create(4, GameMode.Classic));

        Assert.All(ok.State.Players, p => Assert.False(p.IsNeutral));
    }

    [Fact]
    public void Create_deals_all_42_territories_equitably_across_4_players()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(GameSetup.Create(4, GameMode.Classic));

        var counts = ok.State.Territories.Values
            .GroupBy(t => t.Owner!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(4, counts.Count);
        Assert.Equal(42, counts.Values.Sum());
        Assert.All(counts.Values, count => Assert.InRange(count, 10, 11));
    }

    [Theory]
    [InlineData(GameMode.TwoPlayer, 2, 40)]
    [InlineData(GameMode.Classic, 3, 35)]
    [InlineData(GameMode.Classic, 4, 30)]
    [InlineData(GameMode.Classic, 5, 25)]
    public void Create_assigns_the_official_starting_troop_pool(GameMode mode, int playerCount, int startingTroops)
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(GameSetup.Create(playerCount, mode));

        var totalRemaining = ok.State.Players.Sum(p => p.TroopsRemaining);
        var territoriesPlaced = ok.State.Territories.Count; // 1 troop auto-placed per dealt territory

        Assert.Equal(playerCount * startingTroops, totalRemaining + territoriesPlaced);
    }

    [Fact]
    public void Turn_based_placement_ends_only_when_all_players_reach_zero_remaining_troops()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(GameSetup.Create(2, GameMode.TwoPlayer));
        var state = ok.State;
        var engine = new Risk.Engine.GameEngine(new Risk.Tests.Fakes.QueuedDiceRoller());

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
