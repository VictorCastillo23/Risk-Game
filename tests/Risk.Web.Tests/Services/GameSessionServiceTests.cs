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
        var session = new GameSessionService(new FakeGameEngine());
        var raised = false;
        session.Changed += () => raised = true;

        var result = session.Start(TwoValidRows);

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
        var session = new GameSessionService(new FakeGameEngine());
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
        var session = new GameSessionService(engine);
        session.Start(TwoValidRows);
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
        var session = new GameSessionService(engine);
        session.Start(TwoValidRows);
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
        var session = new GameSessionService(new FakeGameEngine());
        session.Start(TwoValidRows);

        session.Reset();

        Assert.False(session.IsStarted);
        Assert.Null(session.State);
        Assert.Empty(session.Players);
    }
}
