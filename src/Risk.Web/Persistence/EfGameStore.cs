using Microsoft.EntityFrameworkCore;
using Risk.Engine.State;
using Risk.Web.Data;

namespace Risk.Web.Persistence;

/// <summary>
/// <see cref="IGameStore"/> implementation against <see cref="RiskDbContext"/>.
/// Security-relevant invariant: EVERY query is scoped by
/// <c>.Where(sg =&gt; sg.OwnerId == userId)</c> (here, via <c>FindAsync</c> on
/// the primary key, which is exactly <c>OwnerId</c> — see
/// <see cref="RiskDbContext"/>'s shared-primary-key mapping) — never trusts a
/// caller-supplied id alone without this filter, so one account can never
/// load/delete another account's save.
/// </summary>
public sealed class EfGameStore(RiskDbContext db) : IGameStore
{
    public async Task<SavedGameSummary?> GetSummaryAsync(string userId, CancellationToken ct = default)
    {
        var row = await db.SavedGames
            .Where(sg => sg.OwnerId == userId)
            .Select(sg => new { sg.Mode, sg.PlayerCount, sg.Phase, sg.SavedAtUtc, sg.SchemaVersion })
            .SingleOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        return new SavedGameSummary(
            Enum.Parse<GameMode>(row.Mode),
            row.PlayerCount,
            Enum.Parse<TurnPhase>(row.Phase),
            row.SavedAtUtc,
            row.SchemaVersion == GameSnapshot.CurrentSchemaVersion);
    }

    public async Task<GameSnapshot?> LoadAsync(string userId, CancellationToken ct = default)
    {
        var row = await db.SavedGames
            .Where(sg => sg.OwnerId == userId)
            .SingleOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        var state = GameSnapshotSerializer.DeserializeState(row.StateJson);
        var players = GameSnapshotSerializer.DeserializePlayers(row.PlayersJson);
        return new GameSnapshot(state, players);
    }

    public async Task<bool> SaveAsync(string userId, GameSnapshot snapshot, CancellationToken ct = default)
    {
        var existing = await db.SavedGames
            .Where(sg => sg.OwnerId == userId)
            .SingleOrDefaultAsync(ct);

        var stateJson = GameSnapshotSerializer.SerializeState(snapshot.State);
        var playersJson = GameSnapshotSerializer.SerializePlayers(snapshot.Players);
        var mode = snapshot.State.Mode.ToString();
        var phase = snapshot.State.Turn.Phase.ToString();
        var savedAtUtc = DateTime.UtcNow;

        if (existing is null)
        {
            db.SavedGames.Add(new SavedGame
            {
                OwnerId = userId,
                StateJson = stateJson,
                PlayersJson = playersJson,
                Mode = mode,
                PlayerCount = snapshot.Players.Count,
                Phase = phase,
                SavedAtUtc = savedAtUtc,
                SchemaVersion = GameSnapshot.CurrentSchemaVersion,
            });
            await db.SaveChangesAsync(ct);
            return false;
        }

        existing.StateJson = stateJson;
        existing.PlayersJson = playersJson;
        existing.Mode = mode;
        existing.PlayerCount = snapshot.Players.Count;
        existing.Phase = phase;
        existing.SavedAtUtc = savedAtUtc;
        existing.SchemaVersion = GameSnapshot.CurrentSchemaVersion;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task DeleteAsync(string userId, CancellationToken ct = default)
    {
        var existing = await db.SavedGames
            .Where(sg => sg.OwnerId == userId)
            .SingleOrDefaultAsync(ct);

        if (existing is null)
        {
            return;
        }

        db.SavedGames.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
