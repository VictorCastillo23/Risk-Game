using Risk.Web.Persistence;

namespace Risk.Web.Tests.Fakes;

/// <summary>
/// Decorator over another <see cref="IGameStore"/> (usually
/// <see cref="FakeGameStore"/>) that throws from whichever methods are
/// opted in via its mutable <c>ThrowOn*</c> flags, simulating an infra
/// failure (DB down/timeout/pool exhaustion) for
/// <c>GameSessionService</c>'s failure-safe save/resume/delete contract
/// (PR5 fix pass, BLOCKER finding). Flags are mutable rather than
/// constructor-only so a single test can prove "first save succeeds, second
/// save fails" without needing a second store instance or losing the first
/// save's effect on the wrapped <paramref name="inner"/> store.
/// </summary>
internal sealed class ThrowingGameStore(IGameStore inner) : IGameStore
{
    public bool ThrowOnSave { get; set; }

    public bool ThrowOnLoad { get; set; }

    public bool ThrowOnDelete { get; set; }

    public bool ThrowOnGetSummary { get; set; }

    public Exception Exception { get; set; } = new InvalidOperationException("Simulated store failure.");

    public Task<SavedGameSummary?> GetSummaryAsync(string userId, CancellationToken ct = default) =>
        ThrowOnGetSummary ? throw Exception : inner.GetSummaryAsync(userId, ct);

    public Task<GameSnapshot?> LoadAsync(string userId, CancellationToken ct = default) =>
        ThrowOnLoad ? throw Exception : inner.LoadAsync(userId, ct);

    public Task<bool> SaveAsync(string userId, GameSnapshot snapshot, CancellationToken ct = default) =>
        ThrowOnSave ? throw Exception : inner.SaveAsync(userId, snapshot, ct);

    public Task DeleteAsync(string userId, CancellationToken ct = default) =>
        ThrowOnDelete ? throw Exception : inner.DeleteAsync(userId, ct);
}
