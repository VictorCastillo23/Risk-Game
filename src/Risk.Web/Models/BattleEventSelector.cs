using Risk.Engine.Events;

namespace Risk.Web.Models;

/// <summary>
/// Pure selection of the most recent <see cref="BattleResolved"/> event out
/// of a batch of events (e.g. <c>GameSessionService.LastEvents</c>), for
/// <c>DicePanel</c> to render. A single <c>AttackCommand</c> only ever
/// produces at most one <see cref="BattleResolved"/> per call, but this
/// stays list-based (rather than assuming position/count) so it keeps
/// working if the engine's per-call event shape ever changes.
/// </summary>
public static class BattleEventSelector
{
    /// <summary>The last <see cref="BattleResolved"/> in <paramref name="events"/>, or <c>null</c> if none is present.</summary>
    public static BattleResolved? MostRecent(IReadOnlyList<GameEvent> events) =>
        events.OfType<BattleResolved>().LastOrDefault();
}
