using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Web.Models;

/// <summary>One player's derived totals for the persistent stats bar.</summary>
public readonly record struct PlayerStatsRow(PlayerId Player, int TerritoryCount, int TroopCount);

/// <summary>
/// Pure derivation of per-player totals (territories owned, troops summed
/// across those territories) from a live <see cref="GameState"/>, backing
/// the always-visible stats bar. Design's read convention (D5): board/player
/// counts are PUBLIC state, already visible on the board itself — read
/// straight off <see cref="GameState"/>, never through <c>PlayerView</c>.
/// Eliminated players are omitted (by the time <c>PlayerEliminated</c>
/// fires, all their territories have already been captured one at a time,
/// so a lingering 0/0 row is redundant clutter); the neutral army
/// (<see cref="GameMode.TwoPlayer"/>) is included, since it owns territories
/// that count toward the board's totals and is already visibly colored via
/// its own synthesized <c>PlayerConfig</c> (<c>GameSessionService.Start</c>).
/// </summary>
public static class PlayerStats
{
    /// <summary>One row per non-eliminated player, in <see cref="GameState.Players"/> order.</summary>
    public static IReadOnlyList<PlayerStatsRow> Rows(GameState state) =>
        state.Players
            .Where(player => !player.IsEliminated)
            .Select(player => new PlayerStatsRow(player.Id, TerritoryCount(state, player.Id), TroopCount(state, player.Id)))
            .ToArray();

    private static int TerritoryCount(GameState state, PlayerId player) =>
        state.Territories.Values.Count(territory => territory.Owner == player);

    private static int TroopCount(GameState state, PlayerId player) =>
        state.Territories.Values.Where(territory => territory.Owner == player).Sum(territory => territory.Troops);
}
