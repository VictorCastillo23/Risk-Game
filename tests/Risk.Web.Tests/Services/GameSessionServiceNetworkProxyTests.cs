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

/// <summary>
/// The networked proxy seam: when this circuit joined a networked game,
/// <see cref="GameSessionService"/> reads/forwards through that
/// <see cref="NetworkGameSession"/> so every existing panel works
/// unchanged; otherwise it behaves exactly like hot-seat.
/// </summary>
public class GameSessionServiceNetworkProxyTests
{
    private sealed record Harness(
        GameSessionService Service,
        NetworkGameSession Net,
        NetworkedSeatContext Context,
        FakeGameEngine Engine);

    private static Harness NewProxied(IDiceRoller dice)
    {
        var engine = new FakeGameEngine();
        var registry = new NetworkGameRegistry(engine, dice);
        var net = registry.Create();
        var context = new NetworkedSeatContext { Code = net.Code };
        var service = new GameSessionService(
            engine, dice, new FakeGameStore(), StubAuthenticationStateProvider.Anonymous(),
            registry, context);
        return new Harness(service, net, context, engine);
    }

    private static void StartNetTwoPlayer(Harness h)
    {
        var ana = h.Net.ClaimSeat("Ana", "#FF0000", "c0");
        h.Net.ClaimSeat("Beto", "#00FF00", "c1");
        h.Context.Seat = ana;
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(h.Net.Start(GameMode.TwoPlayer));
    }

    private static PlayerView ViewFor(GameState state) => new(
        state.Territories, [], new Dictionary<PlayerId, int>(),
        state.Turn, null, new Dictionary<PlayerId, TerritoryId>(), null);

    [Fact]
    public void State_And_Players_ProxyNetworkSession_WhenJoined()
    {
        var h = NewProxied(QueuedDiceRoller.ForRollOff(2));
        Assert.Null(h.Service.State);

        StartNetTwoPlayer(h);

        Assert.NotNull(h.Service.State);
        Assert.Same(h.Net.State, h.Service.State);
        Assert.Equal("Ana", h.Service.ConfigFor(new PlayerId(0)).Name);
        Assert.True(h.Service.IsStarted);
    }

    [Fact]
    public void Execute_ForwardsDispatch_AndUpdatesNetworkState()
    {
        var h = NewProxied(QueuedDiceRoller.ForRollOff(2));
        StartNetTwoPlayer(h);
        var current = h.Net.State!.Turn.CurrentPlayer;
        var next = h.Net.State with { TradesCompleted = 7 };
        h.Engine.ExecuteResult = new CommandResult<GameState, GameEvent>.Ok(next, []);

        var result = h.Service.Execute(new EndPhaseCommand(current));

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Same(next, h.Net.State);
        Assert.Same(next, h.Service.State);
    }

    [Fact]
    public void Execute_WrongActor_ReturnsNotYourTurn_WithoutCallingEngine()
    {
        // ExecuteResult deliberately unset: any engine call throws, proving
        // the server-side gate rejected first.
        var h = NewProxied(QueuedDiceRoller.ForRollOff(2));
        StartNetTwoPlayer(h);
        var current = h.Net.State!.Turn.CurrentPlayer;
        var other = current == new PlayerId(0) ? new PlayerId(1) : new PlayerId(0);

        var result = h.Service.Execute(new EndPhaseCommand(other));

        var rejected = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotYourTurn, rejected.Error.Code);
    }

    [Fact]
    public void Changed_RelayedFromNetworkSession()
    {
        var h = NewProxied(QueuedDiceRoller.ForRollOff(2));
        StartNetTwoPlayer(h);
        var current = h.Net.State!.Turn.CurrentPlayer;
        h.Engine.ExecuteResult = new CommandResult<GameState, GameEvent>.Ok(h.Net.State, []);
        var raised = false;
        h.Service.Changed += () => raised = true;

        h.Net.Dispatch(new EndPhaseCommand(current));

        Assert.True(raised);
    }

    [Fact]
    public void ObserveCurrentPlayer_ReturnsCallerSeatView()
    {
        var h = NewProxied(QueuedDiceRoller.ForRollOff(2));
        StartNetTwoPlayer(h);
        h.Engine.ObserveResult = ViewFor(h.Net.State!);

        var view = h.Service.ObserveCurrentPlayer();

        Assert.Same(h.Engine.ObserveResult, view);
        Assert.Equal(new PlayerId(0), h.Engine.LastObserveViewer);
    }

    [Fact]
    public void ObserveCurrentPlayer_WithoutSeat_Throws()
    {
        var h = NewProxied(QueuedDiceRoller.ForRollOff(2));
        h.Net.ClaimSeat("Ana", "#FF0000", "c0");
        h.Net.ClaimSeat("Beto", "#00FF00", "c1");
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(h.Net.Start(GameMode.TwoPlayer));
        // Context.Seat deliberately left null: a seatless viewer must never
        // silently receive another player's hand.

        Assert.Throws<InvalidOperationException>(() => h.Service.ObserveCurrentPlayer());
    }
}
