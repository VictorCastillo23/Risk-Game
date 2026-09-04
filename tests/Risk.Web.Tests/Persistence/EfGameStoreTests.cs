using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Risk.Engine;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Setup;
using Risk.Engine.State;
using Risk.Web.Data;
using Risk.Web.Models;
using Risk.Web.Persistence;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Persistence;

/// <summary>
/// Task 5.4 (RED-first): <see cref="EfGameStore"/> against a real
/// <see cref="RiskDbContext"/> backed by an open in-memory Sqlite connection
/// (same fixture-less pattern as <c>AccountPagesTestFixture</c>, just without
/// a full <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>
/// host — this is a data-layer test, not an HTTP one). Every test seeds a
/// real <c>AspNetUsers</c> row first since <c>SavedGames.OwnerId</c> is a
/// cascade-delete FK into it.
/// </summary>
public sealed class EfGameStoreTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly RiskDbContext _db;
    private readonly EfGameStore _store;

    public EfGameStoreTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<RiskDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new RiskDbContext(options);
        _db.Database.EnsureCreated();
        _store = new EfGameStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private void SeedUser(string userId) =>
        _db.Users.Add(new ApplicationUser { Id = userId, UserName = $"{userId}@test.local" });

    private static GameSnapshot BuildSnapshot()
    {
        var setupResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(2, GameMode.TwoPlayer, new QueuedDiceRoller()));
        var players = new List<PlayerConfig>
        {
            new(new Risk.Domain.Players.PlayerId(0), "Ana", "#E53935", false),
            new(new Risk.Domain.Players.PlayerId(1), "Beto", "#1E88E5", false),
        };
        return new GameSnapshot(setupResult.State, players);
    }

    [Fact]
    public async Task SaveAsync_NoExistingSave_CreatesRowAndReturnsFalse()
    {
        SeedUser("user-1");
        await _db.SaveChangesAsync();

        var overwritten = await _store.SaveAsync("user-1", BuildSnapshot());

        Assert.False(overwritten);
        Assert.Equal(1, await _db.SavedGames.CountAsync());
    }

    [Fact]
    public async Task SaveAsync_ExistingSave_OverwritesAndReturnsTrue()
    {
        SeedUser("user-1");
        await _db.SaveChangesAsync();
        await _store.SaveAsync("user-1", BuildSnapshot());

        var overwritten = await _store.SaveAsync("user-1", BuildSnapshot());

        Assert.True(overwritten);
        Assert.Equal(1, await _db.SavedGames.CountAsync());
    }

    [Fact]
    public async Task LoadAsync_NoSavedGame_ReturnsNull()
    {
        SeedUser("user-1");
        await _db.SaveChangesAsync();

        var loaded = await _store.LoadAsync("user-1");

        Assert.Null(loaded);
    }

    [Fact]
    public async Task LoadAsync_AfterSave_ReturnsEquivalentSnapshot()
    {
        SeedUser("user-1");
        await _db.SaveChangesAsync();
        var snapshot = BuildSnapshot();
        await _store.SaveAsync("user-1", snapshot);

        var loaded = await _store.LoadAsync("user-1");

        Assert.NotNull(loaded);
        Assert.Equal(snapshot.Players.Count, loaded!.Players.Count);
        Assert.Equal(snapshot.State.Mode, loaded.State.Mode);
        Assert.Equal(snapshot.State.Turn.Phase, loaded.State.Turn.Phase);
    }

    [Fact]
    public async Task GetSummaryAsync_NoSavedGame_ReturnsNull()
    {
        SeedUser("user-1");
        await _db.SaveChangesAsync();

        var summary = await _store.GetSummaryAsync("user-1");

        Assert.Null(summary);
    }

    [Fact]
    public async Task GetSummaryAsync_AfterSave_ReturnsDenormalizedFields()
    {
        SeedUser("user-1");
        await _db.SaveChangesAsync();
        var snapshot = BuildSnapshot();
        await _store.SaveAsync("user-1", snapshot);

        var summary = await _store.GetSummaryAsync("user-1");

        Assert.NotNull(summary);
        Assert.Equal(GameMode.TwoPlayer, summary!.Mode);
        Assert.Equal(snapshot.Players.Count, summary.PlayerCount);
        Assert.Equal(snapshot.State.Turn.Phase, summary.Phase);
        Assert.True(summary.IsCompatible);
    }

    [Fact]
    public async Task DeleteAsync_ExistingSave_RemovesRow()
    {
        SeedUser("user-1");
        await _db.SaveChangesAsync();
        await _store.SaveAsync("user-1", BuildSnapshot());

        await _store.DeleteAsync("user-1");

        Assert.Equal(0, await _db.SavedGames.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_NoSavedGame_IsIdempotentNoOp()
    {
        SeedUser("user-1");
        await _db.SaveChangesAsync();

        var exception = await Record.ExceptionAsync(() => _store.DeleteAsync("user-1"));

        Assert.Null(exception);
    }

    /// <summary>
    /// Security-relevant invariant (explicitly required): one account must
    /// never be able to load another account's save even if it queries by
    /// its own (different) id while another user's row exists.
    /// </summary>
    [Fact]
    public async Task LoadAsync_DoesNotLeakAnotherAccountsSave()
    {
        SeedUser("owner");
        SeedUser("intruder");
        await _db.SaveChangesAsync();
        await _store.SaveAsync("owner", BuildSnapshot());

        var intruderLoad = await _store.LoadAsync("intruder");
        var intruderSummary = await _store.GetSummaryAsync("intruder");

        Assert.Null(intruderLoad);
        Assert.Null(intruderSummary);
    }
}
