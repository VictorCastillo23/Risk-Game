using Risk.Web.Persistence;

namespace Risk.Web.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IGameStore"/> test double, following this repo's
/// hand-rolled-fakes-only convention (no Moq/NSubstitute — same shape as
/// <see cref="FakeGameEngine"/>). Keyed by user id so tests can assert
/// isolation between accounts; tracks <see cref="SaveCount"/>/
/// <see cref="LastUserId"/>/<see cref="DeleteCount"/> for call-count/
/// argument assertions.
///
/// Deliberately round-trips every save through the REAL
/// <see cref="GameSnapshotSerializer"/> (serialize on <see cref="SaveAsync"/>,
/// deserialize on <see cref="LoadAsync"/>) instead of holding the
/// <see cref="GameSnapshot"/> object by reference — "in-memory" describes
/// where the JSON lives (a dictionary, not a database), not that
/// serialization is skipped. This is what lets task 5.6's integration test
/// prove the session layer and PR3/4's serializer genuinely compose,
/// without needing a real database.
/// </summary>
internal sealed class FakeGameStore : IGameStore
{
    private sealed record StoredRow(string StateJson, string PlayersJson, DateTime SavedAtUtc, int SchemaVersion);

    private readonly Dictionary<string, StoredRow> _saves = new();

    public int SaveCount { get; private set; }

    public int DeleteCount { get; private set; }

    public string? LastUserId { get; private set; }

    public Task<SavedGameSummary?> GetSummaryAsync(string userId, CancellationToken ct = default)
    {
        LastUserId = userId;

        if (!_saves.TryGetValue(userId, out var row))
        {
            return Task.FromResult<SavedGameSummary?>(null);
        }

        var isCompatible = row.SchemaVersion == GameSnapshot.CurrentSchemaVersion;
        if (!isCompatible)
        {
            // Matches EfGameStore.GetSummaryAsync: the denormalized
            // SchemaVersion column alone drives IsCompatible, never a full
            // deserialize (PR5 fix pass).
            return Task.FromResult<SavedGameSummary?>(new SavedGameSummary(
                default,
                0,
                default,
                row.SavedAtUtc,
                IsCompatible: false));
        }

        var state = GameSnapshotSerializer.DeserializeState(row.StateJson);
        var players = GameSnapshotSerializer.DeserializePlayers(row.PlayersJson);
        var summary = new SavedGameSummary(
            state.Mode,
            players.Count,
            state.Turn.Phase,
            row.SavedAtUtc,
            IsCompatible: true);

        return Task.FromResult<SavedGameSummary?>(summary);
    }

    public Task<GameSnapshot?> LoadAsync(string userId, CancellationToken ct = default)
    {
        LastUserId = userId;

        if (!_saves.TryGetValue(userId, out var row))
        {
            return Task.FromResult<GameSnapshot?>(null);
        }

        if (row.SchemaVersion != GameSnapshot.CurrentSchemaVersion)
        {
            // Matches EfGameStore.LoadAsync: an incompatible/stale schema
            // version is reported as "no save found" rather than attempting
            // a doomed deserialize of a shape that may not parse at all
            // (PR5 fix pass, CRITICAL #2).
            return Task.FromResult<GameSnapshot?>(null);
        }

        var state = GameSnapshotSerializer.DeserializeState(row.StateJson);
        var players = GameSnapshotSerializer.DeserializePlayers(row.PlayersJson);
        return Task.FromResult<GameSnapshot?>(new GameSnapshot(state, players));
    }

    public Task<bool> SaveAsync(string userId, GameSnapshot snapshot, CancellationToken ct = default)
    {
        LastUserId = userId;
        SaveCount++;

        var overwritten = _saves.ContainsKey(userId);
        _saves[userId] = new StoredRow(
            GameSnapshotSerializer.SerializeState(snapshot.State),
            GameSnapshotSerializer.SerializePlayers(snapshot.Players),
            DateTime.UtcNow,
            GameSnapshot.CurrentSchemaVersion);

        return Task.FromResult(overwritten);
    }

    /// <summary>
    /// Test-only seam: seeds a row with a mismatched
    /// <see cref="GameSnapshot.CurrentSchemaVersion"/> and deliberately
    /// invalid JSON payloads, so a test can prove <see cref="LoadAsync"/>/
    /// <see cref="GetSummaryAsync"/> never attempt to deserialize an
    /// incompatible row (PR5 fix pass, CRITICAL #2) — if either method tried
    /// to deserialize "not-valid-json" it would throw, so a passing test
    /// here is proof the schema-version guard runs first.
    /// </summary>
    public void SeedIncompatibleSave(string userId, int schemaVersion)
    {
        _saves[userId] = new StoredRow("not-valid-json", "not-valid-json", DateTime.UtcNow, schemaVersion);
    }

    public Task DeleteAsync(string userId, CancellationToken ct = default)
    {
        LastUserId = userId;
        DeleteCount++;
        _saves.Remove(userId);
        return Task.CompletedTask;
    }
}
