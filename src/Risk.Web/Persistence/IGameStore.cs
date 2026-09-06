using Risk.Engine.State;

namespace Risk.Web.Persistence;

/// <summary>
/// Persists/loads at most one <see cref="GameSnapshot"/> per owning account.
/// <see cref="GameSessionService"/> is the only caller (design D4) — every
/// method takes an explicit <paramref name="userId"/> resolved server-side
/// from <c>AuthenticationStateProvider</c>, never one supplied by a
/// component, and every implementation MUST scope its query by that id (see
/// <see cref="EfGameStore"/>'s security-relevant invariant).
/// </summary>
public interface IGameStore
{
    /// <summary>
    /// A lightweight "do you have a save" check that never deserializes
    /// <see cref="GameSnapshot.State"/>/<see cref="GameSnapshot.Players"/> —
    /// reads only the denormalized <c>SavedGame</c> columns. Returns
    /// <see langword="null"/> if <paramref name="userId"/> has no saved game.
    /// </summary>
    Task<SavedGameSummary?> GetSummaryAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Loads and fully deserializes <paramref name="userId"/>'s saved game.
    /// Returns <see langword="null"/> if none exists.
    /// </summary>
    Task<GameSnapshot?> LoadAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Upserts <paramref name="userId"/>'s saved game: creates a new row if
    /// none exists, overwrites the existing one otherwise (spec's "one saved
    /// game per account, overwritten on re-save"). Returns
    /// <see langword="true"/> if an existing row was overwritten,
    /// <see langword="false"/> if a new row was created — lets the caller
    /// (<see cref="GameSessionService.SaveAsync"/>) report which happened
    /// without a separate round-trip / TOCTOU gap.
    /// </summary>
    Task<bool> SaveAsync(string userId, GameSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Deletes <paramref name="userId"/>'s saved game, if one exists.
    /// Idempotent: a no-op (not an error) when there is nothing to delete.
    /// </summary>
    Task DeleteAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Denormalized summary of a saved game, for a resume list (PR6) to render
/// without paying the cost of deserializing <see cref="GameSnapshot.State"/>.
/// <see cref="IsCompatible"/> compares the saved row's schema version against
/// <see cref="GameSnapshot.CurrentSchemaVersion"/> (design D5) — also derived
/// from a denormalized column, so checking compatibility never requires a
/// full deserialize either.
/// </summary>
public sealed record SavedGameSummary(
    GameMode Mode,
    int PlayerCount,
    TurnPhase Phase,
    DateTime SavedAtUtc,
    bool IsCompatible);
