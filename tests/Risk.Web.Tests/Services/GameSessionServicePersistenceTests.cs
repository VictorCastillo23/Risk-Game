using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
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

    private static GameSessionService NewSession(IGameStore store, Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider auth)
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
    public async Task ResumeAsync_Anonymous_ReturnsNotAuthenticated_AndNeverTouchesStore()
    {
        var store = new FakeGameStore();
        var session = NewSession(store, StubAuthenticationStateProvider.Anonymous());

        var resumed = await session.ResumeAsync();

        Assert.Equal(ResumeOutcome.NotAuthenticated, resumed);
        Assert.Null(store.LastUserId);
        Assert.False(session.IsStarted);
    }

    [Fact]
    public async Task ResumeAsync_SignedInNoSave_ReturnsNoSavedGame()
    {
        var store = new FakeGameStore();
        var session = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));

        var resumed = await session.ResumeAsync();

        Assert.Equal(ResumeOutcome.NoSavedGame, resumed);
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

        Assert.Equal(ResumeOutcome.Resumed, resumed);
        Assert.True(resumer.IsStarted);
        Assert.Equal("user-1", resumer.OwnerUserId);
        Assert.Equal("Ana", resumer.ConfigFor(new PlayerId(0)).Name);
    }

    /// <summary>
    /// CRITICAL #2 (PR5 fix pass): a schema-version-incompatible row must
    /// resolve to the same caller-facing outcome as "no save exists" —
    /// never a thrown exception from a doomed deserialize.
    /// </summary>
    [Fact]
    public async Task ResumeAsync_IncompatibleSchemaVersion_ReturnsNoSavedGame_WithoutThrowing()
    {
        var store = new FakeGameStore();
        store.SeedIncompatibleSave("user-1", GameSnapshot.CurrentSchemaVersion + 1);
        var session = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));

        var resumed = await session.ResumeAsync();

        Assert.Equal(ResumeOutcome.NoSavedGame, resumed);
        Assert.False(session.IsStarted);
    }

    /// <summary>
    /// BLOCKER finding (PR5 fresh-context review): an unhandled infra
    /// exception (DB down/timeout) from <see cref="IGameStore.LoadAsync"/>
    /// must never propagate out of <see cref="GameSessionService.ResumeAsync"/>
    /// — that would fault the entire Blazor Server circuit. Resolves to a
    /// clean <see cref="ResumeOutcome.Failed"/> instead, leaving the session
    /// un-started rather than half-initialized.
    /// </summary>
    [Fact]
    public async Task ResumeAsync_StoreThrows_ReturnsFailed_AndDoesNotStartSession()
    {
        var store = new ThrowingGameStore(new FakeGameStore()) { ThrowOnLoad = true };
        var session = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));

        var resumed = await session.ResumeAsync();

        Assert.Equal(ResumeOutcome.Failed, resumed);
        Assert.False(session.IsStarted);
        Assert.Null(session.OwnerUserId);
    }

    /// <summary>
    /// BLOCKER finding, save-path counterpart: an unhandled infra exception
    /// from <see cref="IGameStore.SaveAsync"/> must resolve to
    /// <see cref="SaveOutcome.Failed"/>, never throw — and the in-progress,
    /// unsaved game (<see cref="GameSessionService.State"/>) must remain
    /// exactly as it was, still the same object, still playable.
    /// </summary>
    [Fact]
    public async Task SaveAsync_StoreThrows_ReturnsFailed_AndLeavesStateAndOwnerUnchanged()
    {
        var store = new ThrowingGameStore(new FakeGameStore()) { ThrowOnSave = true };
        var session = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));
        session.Start(TwoValidRows, GameMode.TwoPlayer);
        var stateBeforeSave = session.State;

        var outcome = await session.SaveAsync();

        Assert.Equal(SaveOutcome.Failed, outcome);
        Assert.Null(session.OwnerUserId);
        Assert.False(session.HasPersistedSave);
        Assert.Same(stateBeforeSave, session.State);

        // Still playable: the failed save must not have corrupted anything
        // that would prevent a further command from being executed.
        var actor = session.State!.Turn.CurrentPlayer;
        var actorPool = session.State!.Players.Single(p => p.Id == actor).TroopsRemaining;
        var playResult = actorPool > 0
            ? session.Execute(new PlaceTroopsCommand(actor, session.State!.Territories.First(kv => kv.Value.Owner == actor).Key, 1))
            : session.Execute(new PlaceNeutralTroopsCommand(
                actor,
                session.State!.Territories.First(kv => kv.Value.Owner == session.State!.Players.Single(p => p.IsNeutral).Id).Key,
                1));
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(playResult);
    }

    /// <summary>
    /// BLOCKER finding: a save failure must never clear a previously-set
    /// <see cref="GameSessionService.OwnerUserId"/> from an earlier
    /// successful save on the same session.
    /// </summary>
    [Fact]
    public async Task SaveAsync_StoreThrowsOnSecondSave_DoesNotClearPreviousOwnerUserId()
    {
        var store = new ThrowingGameStore(new FakeGameStore());
        var session = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));
        session.Start(TwoValidRows, GameMode.TwoPlayer);
        var firstOutcome = await session.SaveAsync();
        Assert.Equal(SaveOutcome.Created, firstOutcome);
        Assert.Equal("user-1", session.OwnerUserId);

        store.ThrowOnSave = true;
        var secondOutcome = await session.SaveAsync();

        Assert.Equal(SaveOutcome.Failed, secondOutcome);
        Assert.Equal("user-1", session.OwnerUserId);
    }

    /// <summary>
    /// CRITICAL #1 (PR5 fresh-context review): a DB hiccup while deleting a
    /// now-stale save row after a win must never throw — this is cosmetic
    /// housekeeping riding on the most important terminal-state UI (the
    /// victory screen), and must be strictly best-effort.
    /// </summary>
    [Fact]
    public async Task DeleteSaveIfWonAsync_StoreThrows_NeverThrows()
    {
        var store = new ThrowingGameStore(new FakeGameStore()) { ThrowOnDelete = true };
        var engineFake = new FakeGameEngine();
        var session = new GameSessionService(engineFake, new AlwaysAttackerWinsDiceRoller(), store, StubAuthenticationStateProvider.SignedIn("user-1"));
        session.Start(TwoValidRows, GameMode.TwoPlayer);
        await session.SaveAsync();
        var wonState = session.State! with { Status = new GameStatus.Won(session.State!.Turn.CurrentPlayer) };
        engineFake.ExecuteResult = new CommandResult<GameState, GameEvent>.Ok(wonState, []);
        session.Execute(new EndPhaseCommand(session.State!.Turn.CurrentPlayer));

        var exception = await Record.ExceptionAsync(() => session.DeleteSaveIfWonAsync());

        Assert.Null(exception);
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

    /// <summary>
    /// Task 6.2's confirm-before-overwrite pre-check: unlike
    /// <see cref="GameSessionService.HasPersistedSave"/> (this session's own
    /// successful save/resume), <see cref="GameSessionService.GetSaveSummaryAsync"/>
    /// asks the store directly for the signed-in ACCOUNT's saved row, so
    /// <c>SavePanel</c> gets an accurate "you already have a save" signal
    /// even on a session that never itself saved before.
    /// </summary>
    [Fact]
    public async Task GetSaveSummaryAsync_Anonymous_ReturnsNull_AndNeverTouchesStore()
    {
        var store = new FakeGameStore();
        var session = NewSession(store, StubAuthenticationStateProvider.Anonymous());

        var summary = await session.GetSaveSummaryAsync();

        Assert.Null(summary);
        Assert.Null(store.LastUserId);
    }

    [Fact]
    public async Task GetSaveSummaryAsync_SignedInNoSave_ReturnsNull()
    {
        var store = new FakeGameStore();
        var session = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));

        var summary = await session.GetSaveSummaryAsync();

        Assert.Null(summary);
    }

    [Fact]
    public async Task GetSaveSummaryAsync_SignedInWithSave_ReturnsSummary()
    {
        var store = new FakeGameStore();
        var saver = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));
        saver.Start(TwoValidRows, GameMode.TwoPlayer);
        await saver.SaveAsync();

        var checker = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));
        var summary = await checker.GetSaveSummaryAsync();

        Assert.NotNull(summary);
        Assert.True(summary!.IsCompatible);
        Assert.Equal(GameMode.TwoPlayer, summary.Mode);
    }

    /// <summary>
    /// Same failure-safe contract as <see cref="SaveAsync_StoreThrows_ReturnsFailed_AndLeavesStateAndOwnerUnchanged"/>:
    /// a confirm-before-overwrite pre-check must never crash the save
    /// button — an infra hiccup here should read the same as "no
    /// conflicting save known", not throw.
    /// </summary>
    [Fact]
    public async Task GetSaveSummaryAsync_StoreThrows_ReturnsNull_WithoutThrowing()
    {
        var store = new ThrowingGameStore(new FakeGameStore()) { ThrowOnGetSummary = true };
        var session = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));

        var summary = await session.GetSaveSummaryAsync();

        Assert.Null(summary);
    }

    /// <summary>
    /// Task 6.4: completes the anonymous-save-then-login flow in one call —
    /// <c>Game.razor</c>'s pending-save rehydration composes exactly
    /// <see cref="GameSessionService.LoadFrom"/> then
    /// <see cref="GameSessionService.SaveAsync"/>, but as one seam on
    /// <see cref="GameSessionService"/> itself (this codebase's "single
    /// stateful seam" convention) rather than duplicated Razor code-behind
    /// logic, and so it's unit-testable without any Razor/JS-interop
    /// machinery.
    /// </summary>
    [Fact]
    public async Task RehydrateAndSaveAsync_LoadsSnapshotAndPersistsIt_ReturnsCreated()
    {
        var store = new FakeGameStore();
        var producer = NewSession(store, StubAuthenticationStateProvider.Anonymous());
        producer.Start(TwoValidRows, GameMode.TwoPlayer);
        var snapshot = producer.Snapshot();

        var consumer = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));

        var outcome = await consumer.RehydrateAndSaveAsync(snapshot);

        Assert.Equal(SaveOutcome.Created, outcome);
        Assert.True(consumer.IsStarted);
        Assert.Equal("user-1", consumer.OwnerUserId);
        Assert.Equal("Ana", consumer.ConfigFor(new PlayerId(0)).Name);
        Assert.Equal(1, store.SaveCount);
    }

    /// <summary>
    /// The rehydrated game must stay playable even if the completing save
    /// fails (BLOCKER-fix parity, PR5): the player just logged in to save a
    /// game they were actively playing — losing it locally on a save
    /// failure would be strictly worse than a failed save alone.
    /// </summary>
    [Fact]
    public async Task RehydrateAndSaveAsync_StoreThrows_StillLoadsState_ButReturnsFailed()
    {
        var store = new ThrowingGameStore(new FakeGameStore()) { ThrowOnSave = true };
        var producer = NewSession(new FakeGameStore(), StubAuthenticationStateProvider.Anonymous());
        producer.Start(TwoValidRows, GameMode.TwoPlayer);
        var snapshot = producer.Snapshot();

        var consumer = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));

        var outcome = await consumer.RehydrateAndSaveAsync(snapshot);

        Assert.Equal(SaveOutcome.Failed, outcome);
        Assert.True(consumer.IsStarted);
        Assert.Null(consumer.OwnerUserId);
    }

    /// <summary>
    /// PR6 fix pass, CRITICAL finding: an anonymous player who wins, then
    /// clicks "Guardar partida" and completes the post-login redirect, would
    /// otherwise have their already-Won game persisted and NEVER cleaned up
    /// — <c>Game.razor</c>'s <c>OnSessionChanged</c> (the only place that
    /// previously called <see cref="GameSessionService.DeleteSaveIfWonAsync"/>)
    /// isn't subscribed yet when <see cref="GameSessionService.LoadFrom"/>
    /// raises <see cref="GameSessionService.Changed"/> synchronously inside
    /// this same call, and <see cref="GameSessionService.OwnerUserId"/> is
    /// still <see langword="null"/> at that exact moment anyway (it's only
    /// set by <see cref="GameSessionService.SaveAsync"/>, which runs after
    /// <see cref="GameSessionService.LoadFrom"/>). Fixed by having
    /// <see cref="GameSessionService.RehydrateAndSaveAsync"/> itself clean up
    /// a just-persisted Won save, instead of relying on an event subscription
    /// whose timing this specific call path can't guarantee.
    /// </summary>
    [Fact]
    public async Task RehydrateAndSaveAsync_WonSnapshot_DeletesAnyPriorSave()
    {
        var store = new FakeGameStore();
        var producer = NewSession(store, StubAuthenticationStateProvider.Anonymous());
        producer.Start(TwoValidRows, GameMode.TwoPlayer);
        var wonState = producer.State! with { Status = new GameStatus.Won(producer.State!.Turn.CurrentPlayer) };
        var wonSnapshot = new GameSnapshot(wonState, producer.Players.Values.ToList());

        var consumer = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));

        var outcome = await consumer.RehydrateAndSaveAsync(wonSnapshot);

        Assert.Equal(SaveOutcome.Created, outcome);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, store.DeleteCount);

        // The row must be genuinely gone, not merely deleted-then-recreated —
        // a second, independent lookup for the same account proves it.
        var checker = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));
        var summary = await checker.GetSaveSummaryAsync();
        Assert.Null(summary);
    }

    /// <summary>
    /// Companion to <see cref="RehydrateAndSaveAsync_WonSnapshot_DeletesAnyPriorSave"/>:
    /// an in-progress (not Won) rehydrated snapshot must NOT trigger a
    /// delete — this is the same "never call the store outside a real win"
    /// contract <see cref="GameSessionService.DeleteSaveIfWonAsync"/> already
    /// has, just re-asserted through the composed <see cref="GameSessionService.RehydrateAndSaveAsync"/>
    /// path.
    /// </summary>
    [Fact]
    public async Task RehydrateAndSaveAsync_InProgressSnapshot_DoesNotDelete()
    {
        var store = new FakeGameStore();
        var producer = NewSession(store, StubAuthenticationStateProvider.Anonymous());
        producer.Start(TwoValidRows, GameMode.TwoPlayer);
        var snapshot = producer.Snapshot();

        var consumer = NewSession(store, StubAuthenticationStateProvider.SignedIn("user-1"));

        var outcome = await consumer.RehydrateAndSaveAsync(snapshot);

        Assert.Equal(SaveOutcome.Created, outcome);
        Assert.Equal(0, store.DeleteCount);
    }
}
