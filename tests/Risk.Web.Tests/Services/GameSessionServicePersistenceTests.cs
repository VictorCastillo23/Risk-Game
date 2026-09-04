using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Web.Models;
using Risk.Web.Persistence;
using Risk.Web.Services;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Services;

/// <summary>
/// Task 5.5: <see cref="GameSessionService"/>'s persistence surface
/// (<see cref="GameSessionService.Snapshot"/>/<see cref="GameSessionService.LoadFrom"/>/
/// <see cref="GameSessionService.SaveAsync"/>/<see cref="GameSessionService.ResumeAsync"/>/
/// <see cref="GameSessionService.DeleteSaveIfWonAsync"/>, plus <see cref="GameSessionService.Reset"/>
/// now raising <see cref="GameSessionService.Changed"/>), exercised entirely
/// through <see cref="FakeGameStore"/>/<see cref="StubAuthenticationStateProvider"/> —
/// no real DB, matching this test class's sibling
/// <see cref="EfGameStoreTests"/>'s split (data-layer vs. session-layer).
/// </summary>
public class GameSessionServicePersistenceTests
{
    private static readonly IReadOnlyList<PlayerSetupRow> TwoValidRows =
    [
        new PlayerSetupRow("Ana", "#FF0000", false),
        new PlayerSetupRow("Beto", "#00FF00", false)
    ];

    private static GameSessionService NewSession(FakeGameStore store, Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider auth)
    {
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        return new GameSessionService(engine, new AlwaysAttackerWinsDiceRoller(), store, auth);
    }

    [Fact]
    public async Task SaveAsync_Anonymous_ReturnsNotAuthenticated_AndNeverTouchesStore()
    {
        var store = new FakeGameStore();
        var session = NewSession(store, StubAuthenticationStateProvider.Anonymous());
        session.Start(TwoValidRows, GameMode.TwoPlayer);

        var outcome = await session.SaveAsync();

        Assert.Equal(SaveOutcome.NotAuthenticated, outcome);
        Assert.Equal(0, store.SaveCount);
        Assert.False(session.HasPersistedSave);
    }

    [Fact]
    public async Task SaveAsync_SignedIn_FirstSave_ReturnsCreated_AndSetsOwnerUserId()
    {
        var store = new FakeGameStore();
        var session = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));
        session.Start(TwoValidRows, GameMode.TwoPlayer);

        var outcome = await session.SaveAsync();

        Assert.Equal(SaveOutcome.Created, outcome);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal("user-1", store.LastUserId);
        Assert.True(session.HasPersistedSave);
        Assert.Equal("user-1", session.OwnerUserId);
    }

    [Fact]
    public async Task SaveAsync_SignedIn_SecondSave_ReturnsOverwritten()
    {
        var store = new FakeGameStore();
        var session = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));
        session.Start(TwoValidRows, GameMode.TwoPlayer);
        await session.SaveAsync();

        var outcome = await session.SaveAsync();

        Assert.Equal(SaveOutcome.Overwritten, outcome);
        Assert.Equal(2, store.SaveCount);
    }

    [Fact]
    public void Snapshot_BeforeStart_Throws()
    {
        var store = new FakeGameStore();
        var session = NewSession(store, StubAuthenticationStateProvider.Anonymous());

        Assert.Throws<InvalidOperationException>(() => session.Snapshot());
    }

    [Fact]
    public void Snapshot_AfterStart_CapturesStateAndPlayers()
    {
        var store = new FakeGameStore();
        var session = NewSession(store, StubAuthenticationStateProvider.Anonymous());
        session.Start(TwoValidRows, GameMode.TwoPlayer);

        var snapshot = session.Snapshot();

        Assert.Same(session.State, snapshot.State);
        Assert.Equal(session.Players.Count, snapshot.Players.Count);
    }

    [Fact]
    public void LoadFrom_RestoresStateAndPlayers_AndRaisesChanged()
    {
        var store = new FakeGameStore();
        var producer = NewSession(store, StubAuthenticationStateProvider.Anonymous());
        producer.Start(TwoValidRows, GameMode.TwoPlayer);
        var snapshot = producer.Snapshot();

        var consumer = NewSession(store, StubAuthenticationStateProvider.Anonymous());
        var raised = false;
        consumer.Changed += () => raised = true;

        consumer.LoadFrom(snapshot);

        Assert.True(consumer.IsStarted);
        Assert.Same(snapshot.State, consumer.State);
        Assert.Equal(snapshot.Players.Count, consumer.Players.Count);
        Assert.Equal("Ana", consumer.ConfigFor(new PlayerId(0)).Name);
        Assert.True(raised);
    }

    [Fact]
    public async Task ResumeAsync_Anonymous_ReturnsFalse_AndNeverTouchesStore()
    {
        var store = new FakeGameStore();
        var session = NewSession(store, StubAuthenticationStateProvider.Anonymous());

        var resumed = await session.ResumeAsync();

        Assert.False(resumed);
        Assert.Null(store.LastUserId);
        Assert.False(session.IsStarted);
    }

    [Fact]
    public async Task ResumeAsync_SignedInNoSave_ReturnsFalse()
    {
        var store = new FakeGameStore();
        var session = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));

        var resumed = await session.ResumeAsync();

        Assert.False(resumed);
        Assert.False(session.IsStarted);
    }

    [Fact]
    public async Task ResumeAsync_SignedInWithSave_LoadsSnapshotAndSetsOwnerUserId()
    {
        var store = new FakeGameStore();
        var saver = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));
        saver.Start(TwoValidRows, GameMode.TwoPlayer);
        await saver.SaveAsync();

        var resumer = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));
        var resumed = await resumer.ResumeAsync();

        Assert.True(resumed);
        Assert.True(resumer.IsStarted);
        Assert.Equal("user-1", resumer.OwnerUserId);
        Assert.Equal("Ana", resumer.ConfigFor(new PlayerId(0)).Name);
    }

    [Fact]
    public async Task DeleteSaveIfWonAsync_InProgressGame_IsNoOp()
    {
        var store = new FakeGameStore();
        var session = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));
        session.Start(TwoValidRows, GameMode.TwoPlayer);
        await session.SaveAsync();

        await session.DeleteSaveIfWonAsync();

        Assert.Equal(0, store.DeleteCount);
    }

    /// <summary>
    /// Drives to <see cref="GameStatus.Won"/> via a <see cref="FakeGameEngine"/>
    /// (mirrors <c>GameSessionServiceTests.WinnerMission_ReturnsWinnersEffectiveMission_OnceWon</c>'s
    /// technique) rather than playing an entire game to completion — only
    /// the Won transition itself matters to this test.
    /// </summary>
    [Fact]
    public async Task DeleteSaveIfWonAsync_AnonymousWonGame_IsNoOp()
    {
        var store = new FakeGameStore();
        var engineFake = new FakeGameEngine();
        var session = new GameSessionService(engineFake, new AlwaysAttackerWinsDiceRoller(), store, StubAuthenticationStateProvider.Anonymous());
        session.Start(TwoValidRows, GameMode.TwoPlayer);
        var wonState = session.State! with { Status = new GameStatus.Won(session.State!.Turn.CurrentPlayer) };
        engineFake.ExecuteResult = new CommandResult<GameState, GameEvent>.Ok(wonState, []);
        session.Execute(new Risk.Engine.Commands.EndPhaseCommand(session.State!.Turn.CurrentPlayer));

        await session.DeleteSaveIfWonAsync();

        Assert.Equal(0, store.DeleteCount);
    }

    [Fact]
    public async Task DeleteSaveIfWonAsync_WonGameWithPersistedSave_DeletesRowForOwner()
    {
        var store = new FakeGameStore();
        var engineFake = new FakeGameEngine();
        var session = new GameSessionService(engineFake, new AlwaysAttackerWinsDiceRoller(), store, StubAuthenticationStateProvider.SignedIn("user-1"));
        session.Start(TwoValidRows, GameMode.TwoPlayer);
        await session.SaveAsync();

        var wonState = session.State! with { Status = new GameStatus.Won(session.State!.Turn.CurrentPlayer) };
        engineFake.ExecuteResult = new CommandResult<GameState, GameEvent>.Ok(wonState, []);
        session.Execute(new Risk.Engine.Commands.EndPhaseCommand(session.State!.Turn.CurrentPlayer));

        await session.DeleteSaveIfWonAsync();

        Assert.Equal(1, store.DeleteCount);
        Assert.Equal("user-1", store.LastUserId);
    }

    [Fact]
    public void Reset_RaisesChanged()
    {
        var store = new FakeGameStore();
        var session = NewSession(store, StubAuthenticationStateProvider.Anonymous());
        session.Start(TwoValidRows, GameMode.TwoPlayer);
        var raised = false;
        session.Changed += () => raised = true;

        session.Reset();

        Assert.True(raised);
        Assert.False(session.IsStarted);
    }

    [Fact]
    public void Reset_ClearsOwnerUserId()
    {
        var store = new FakeGameStore();
        var session = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));
        session.Start(TwoValidRows, GameMode.TwoPlayer);

        session.Reset();

        Assert.Null(session.OwnerUserId);
        Assert.False(session.HasPersistedSave);
    }
}
