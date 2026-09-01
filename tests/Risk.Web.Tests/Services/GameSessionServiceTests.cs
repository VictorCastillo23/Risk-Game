using Risk.Domain.Errors;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Web.Models;
using Risk.Web.Services;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Services;

public class GameSessionServiceTests
{
    private static readonly IReadOnlyList<PlayerSetupRow> TwoValidRows =
    [
        new PlayerSetupRow("Ana", "#FF0000", false),
        new PlayerSetupRow("Beto", "#00FF00", false)
    ];

    [Fact]
    public void Start_WithValidRows_SetsStateAndPlayersAndRaisesChanged()
    {
        var session = new GameSessionService(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(2));
        var raised = false;
        session.Changed += () => raised = true;

        var result = session.Start(TwoValidRows, GameMode.TwoPlayer);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.True(session.IsStarted);
        Assert.NotNull(session.State);
        Assert.True(raised);
        Assert.Equal("Ana", session.ConfigFor(new PlayerId(0)).Name);
        Assert.Equal("Beto", session.ConfigFor(new PlayerId(1)).Name);
    }

    [Fact]
    public void Start_WithInvalidPlayerCount_LeavesStateNullAndDoesNotRaiseChanged()
    {
        // Rejected before dice is ever touched (1 player is illegal for the
        // default Classic mode), so an empty roller is safe here.
        var session = new GameSessionService(new FakeGameEngine(), new QueuedDiceRoller());
        var raised = false;
        session.Changed += () => raised = true;

        var result = session.Start([new PlayerSetupRow("Solo", "#FF0000", false)]);

        Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.False(session.IsStarted);
        Assert.Null(session.State);
        Assert.False(raised);
    }

    [Fact]
    public void Execute_Ok_MutatesStateAndRaisesChanged()
    {
        var engine = new FakeGameEngine();
        var session = new GameSessionService(engine, QueuedDiceRoller.ForRollOff(2));
        session.Start(TwoValidRows, GameMode.TwoPlayer);
        var stateAfterStart = session.State!;
        var newState = stateAfterStart with { TradesCompleted = 1 };
        engine.ExecuteResult = new CommandResult<GameState, GameEvent>.Ok(newState, []);
        var raised = false;
        session.Changed += () => raised = true;

        var command = new PlaceTroopsCommand(new PlayerId(0), new TerritoryId("Alaska"), 1);
        var result = session.Execute(command);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Same(newState, session.State);
        Assert.True(raised);
        Assert.Same(command, engine.LastCommand);
    }

    [Fact]
    public void Execute_Rejected_LeavesStateUnchangedAndDoesNotRaiseChanged()
    {
        var engine = new FakeGameEngine();
        var session = new GameSessionService(engine, QueuedDiceRoller.ForRollOff(2));
        session.Start(TwoValidRows, GameMode.TwoPlayer);
        var stateAfterStart = session.State!;
        engine.ExecuteResult = new CommandResult<GameState, GameEvent>.Rejected(
            new GameError(GameErrorCode.NotYourTurn, "not your turn"));
        var raised = false;
        session.Changed += () => raised = true;

        var result = session.Execute(new PlaceTroopsCommand(new PlayerId(0), new TerritoryId("Alaska"), 1));

        Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Same(stateAfterStart, session.State);
        Assert.False(raised);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var session = new GameSessionService(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(2));
        session.Start(TwoValidRows, GameMode.TwoPlayer);

        session.Reset();

        Assert.False(session.IsStarted);
        Assert.Null(session.State);
        Assert.Empty(session.Players);
    }

    [Theory]
    [InlineData(GameMode.TwoPlayer, 2)]
    [InlineData(GameMode.Classic, 3)]
    [InlineData(GameMode.Classic, 4)]
    [InlineData(GameMode.Classic, 5)]
    public void Start_ZipsRowsToPlayerIdInOrder_ForEveryValidPlayerCount(GameMode mode, int playerCount)
    {
        var rows = Enumerable.Range(0, playerCount)
            .Select(i => new PlayerSetupRow(
                $"Player{i}",
                PlayerPalette.Swatches[i % PlayerPalette.Swatches.Count],
                false))
            .ToArray();
        var session = new GameSessionService(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(playerCount));

        var result = session.Start(rows, mode);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        for (var i = 0; i < playerCount; i++)
        {
            var config = session.ConfigFor(new PlayerId(i));
            Assert.Equal(rows[i].Name, config.Name);
            Assert.Equal(rows[i].ColorHex, config.ColorHex);
        }
    }

    [Fact]
    public void Start_WithClassicMode_StartsInClaimPhase()
    {
        var rows = Enumerable.Range(0, 3)
            .Select(i => new PlayerSetupRow(
                $"Player{i}",
                PlayerPalette.Swatches[i % PlayerPalette.Swatches.Count],
                false))
            .ToArray();
        var session = new GameSessionService(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(3));

        var result = session.Start(rows, GameMode.Classic);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(TurnPhase.Claim, session.State!.Turn.Phase);
    }

    [Fact]
    public void Start_WithTwoPlayerMode_SynthesizesNeutralPlayerConfig()
    {
        var session = new GameSessionService(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(2));

        var result = session.Start(TwoValidRows, GameMode.TwoPlayer);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var neutral = session.State!.Players.Single(p => p.IsNeutral);
        var config = session.ConfigFor(neutral.Id);
        Assert.Equal(BoardColors.NeutralColor, config.ColorHex);
        Assert.False(config.IsAi);
        Assert.Equal(3, session.Players.Count);
    }

    [Fact]
    public void Start_WithClassicMode_DoesNotSynthesizeAnyNeutralPlayerConfig()
    {
        var rows = Enumerable.Range(0, 3)
            .Select(i => new PlayerSetupRow(
                $"Player{i}",
                PlayerPalette.Swatches[i % PlayerPalette.Swatches.Count],
                false))
            .ToArray();
        var session = new GameSessionService(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(3));

        var result = session.Start(rows, GameMode.Classic);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(3, session.Players.Count);
        Assert.DoesNotContain(session.Players.Values, config => config.ColorHex == BoardColors.NeutralColor);
    }

    [Fact]
    public void Start_RejectsSixPlayersUnderClassicMode()
    {
        var rows = Enumerable.Range(0, 6)
            .Select(i => new PlayerSetupRow(
                $"Player{i}",
                PlayerPalette.Swatches[i % PlayerPalette.Swatches.Count],
                false))
            .ToArray();
        // Rejected before dice is ever touched (6 players is illegal for
        // Classic), so an empty roller is safe here.
        var session = new GameSessionService(new FakeGameEngine(), new QueuedDiceRoller());

        var result = session.Start(rows, GameMode.Classic);

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidPlayerCount, rejection.Error.Code);
    }
}
