using Risk.Domain.Dice;
using Risk.Domain.Errors;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Engine.Views;
using Risk.Web.Models;
using Risk.Web.Services;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Services;

public class NetworkGameSessionTests
{
    private static readonly JoinCode TestCode = new("R7K9X2");

    private static NetworkGameSession NewSession(IGameEngine engine, IDiceRoller dice) =>
        new(engine, dice, TestCode);

    private static void ClaimTwoSeats(NetworkGameSession session)
    {
        session.ClaimSeat("Ana", "#FF0000", "conn-ana");
        session.ClaimSeat("Beto", "#00FF00", "conn-beto");
    }

    private static void StartTwoPlayer(NetworkGameSession session)
    {
        ClaimTwoSeats(session);
        var result = session.Start(GameMode.TwoPlayer);
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
    }

    [Fact]
    public void ClaimSeat_FirstClaimBecomesHost()
    {
        var session = NewSession(new FakeGameEngine(), new QueuedDiceRoller());

        var ana = session.ClaimSeat("Ana", "#FF0000", "conn-ana");
        var beto = session.ClaimSeat("Beto", "#00FF00", "conn-beto");

        Assert.Equal(new PlayerId(0), ana);
        Assert.Equal(new PlayerId(1), beto);
        Assert.True(session.Seats[0].IsHost);
        Assert.False(session.Seats[1].IsHost);
        Assert.False(session.IsStarted);
    }

    [Fact]
    public void ClaimSeat_SpectatorDoesNotShiftPlayerIds()
    {
        var session = NewSession(new FakeGameEngine(), new QueuedDiceRoller());

        var ana = session.ClaimSeat("Ana", "#FF0000", "conn-ana");
        var spectator = session.ClaimSeat("Miron", "#6C757D", "conn-spec", isSpectator: true);
        var beto = session.ClaimSeat("Beto", "#00FF00", "conn-beto");

        // Engine seats stay zipped 0..N-1 in claim order no matter how many
        // spectators interleave; spectators get negative ids that can never
        // equal Turn.CurrentPlayer, so their commands fail the Dispatch gate.
        Assert.Equal(new PlayerId(0), ana);
        Assert.Equal(new PlayerId(1), beto);
        Assert.True(spectator.Value < 0);
    }

    [Fact]
    public void Leave_Host_PromotesFirstConnectedHuman()
    {
        var session = NewSession(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(2));
        StartTwoPlayer(session);

        session.Leave(new PlayerId(0));

        Assert.False(session.Seats[0].IsHost);
        Assert.True(session.Seats[1].IsHost);
        Assert.Equal("Beto", session.Seats[1].Config.Name);
    }

    [Fact]
    public void Join_WhenNoHost_PromotesJoiningHuman()
    {
        var session = NewSession(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(2));
        StartTwoPlayer(session);
        session.Leave(new PlayerId(1));
        session.Leave(new PlayerId(0));
        Assert.DoesNotContain(session.Seats, s => s.IsHost);

        Assert.True(session.Join(new PlayerId(0), "conn-ana-2"));

        Assert.True(session.Seats[0].IsHost);
    }

    [Fact]
    public void ClaimSeat_FirstSpectator_IsNotHost_ButFirstHumanIs()
    {
        var session = NewSession(new FakeGameEngine(), new QueuedDiceRoller());

        var spec = session.ClaimSeat("Miron", "#6C757D", "conn-spec", isSpectator: true);
        var ana = session.ClaimSeat("Ana", "#FF0000", "conn-ana");

        Assert.False(session.Seats[0].IsHost);
        Assert.True(session.Seats[1].IsHost);
        Assert.Equal(new PlayerId(0), ana);
    }

    [Fact]
    public void Restore_SetsStateAndUnboundSeatsAndRaisesChanged()
    {
        var source = NewSession(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(2));
        ClaimTwoSeats(source);
        var started = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(source.Start(GameMode.TwoPlayer));

        var session = NewSession(new FakeGameEngine(), new QueuedDiceRoller());
        var raised = false;
        session.Changed += () => raised = true;

        session.Restore(
            started.State,
            [new PlayerConfig(new PlayerId(0), "Ana", "#FF0000", false),
             new PlayerConfig(new PlayerId(1), "Beto", "#00FF00", false)]);

        Assert.True(session.IsStarted);
        Assert.Same(started.State, session.State);
        Assert.Equal(2, session.Seats.Count);
        Assert.True(session.Seats[0].IsHost);
        Assert.DoesNotContain(session.Seats, s => s.IsConnected);
        Assert.True(raised);
    }

    [Fact]
    public void Start_WithTwoSeats_SetsStateAndRaisesChanged()
    {
        var session = NewSession(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(2));
        ClaimTwoSeats(session);
        var raised = false;
        session.Changed += () => raised = true;

        var result = session.Start(GameMode.TwoPlayer);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.True(session.IsStarted);
        Assert.NotNull(session.State);
        Assert.True(raised);
    }

    [Fact]
    public void Join_BindsNewConnectionToExistingSeat()
    {
        var session = NewSession(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(2));
        StartTwoPlayer(session);

        Assert.True(session.Join(new PlayerId(1), "conn-beto-2"));
        Assert.Equal("conn-beto-2", session.Seats[1].ConnectionId);
        Assert.True(session.Seats[1].IsConnected);
    }

    [Fact]
    public void Join_UnknownSeat_ReturnsFalse()
    {
        var session = NewSession(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(2));
        StartTwoPlayer(session);

        Assert.False(session.Join(new PlayerId(9), "conn-ghost"));
    }

    [Fact]
    public void Leave_FreesBindingButKeepsSeat()
    {
        var session = NewSession(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(2));
        StartTwoPlayer(session);
        var raised = false;
        session.Changed += () => raised = true;

        session.Leave(new PlayerId(0));

        Assert.Null(session.Seats[0].ConnectionId);
        Assert.False(session.Seats[0].IsConnected);
        Assert.Equal("Ana", session.Seats[0].Config.Name);
        Assert.True(raised);
    }

    [Fact]
    public void Dispatch_WrongActor_RejectsWithoutCallingEngine()
    {
        // FakeGameEngine.ExecuteResult is deliberately unset: any engine
        // call would throw, so reaching the assertion proves the session
        // rejected purely on its own server-side turn check.
        var session = NewSession(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(2));
        StartTwoPlayer(session);
        var current = session.State!.Turn.CurrentPlayer;
        var other = current == new PlayerId(0) ? new PlayerId(1) : new PlayerId(0);
        var before = session.State;
        var raised = false;
        session.Changed += () => raised = true;

        var result = session.Dispatch(new EndPhaseCommand(other));

        var rejected = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotYourTurn, rejected.Error.Code);
        Assert.Same(before, session.State);
        Assert.False(raised);
    }

    [Fact]
    public void Dispatch_Ok_UpdatesStateAndRaisesChanged()
    {
        var engine = new FakeGameEngine();
        var session = NewSession(engine, QueuedDiceRoller.ForRollOff(2));
        StartTwoPlayer(session);
        var current = session.State!.Turn.CurrentPlayer;
        var next = session.State with { TradesCompleted = 7 };
        engine.ExecuteResult = new CommandResult<GameState, GameEvent>.Ok(next, []);
        var raised = false;
        session.Changed += () => raised = true;

        var result = session.Dispatch(new EndPhaseCommand(current));

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Same(next, session.State);
        Assert.True(raised);
        Assert.Equal(new EndPhaseCommand(current), engine.LastCommand);
    }

    [Fact]
    public void Observe_RoutesRequestedViewerToEngine()
    {
        var engine = new FakeGameEngine();
        var session = NewSession(engine, QueuedDiceRoller.ForRollOff(2));
        StartTwoPlayer(session);
        var state = session.State!;
        engine.ObserveResult = new PlayerView(
            state.Territories, [], new Dictionary<PlayerId, int>(),
            state.Turn, null, new Dictionary<PlayerId, TerritoryId>(), null);

        session.Observe(new PlayerId(1));

        Assert.Equal(new PlayerId(1), engine.LastObserveViewer);
    }
}
