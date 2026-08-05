namespace Risk.Web.Models;

/// <summary>
/// Pure clamping for the occupy-troop-count input (design's PendingOccupation
/// flow: bounds are <c>[PendingOccupation.MinimumTroops, sourceTerritory.Troops - 1]</c>,
/// never re-derived rules — the engine independently enforces them regardless).
/// </summary>
public static class OccupyBounds
{
    /// <summary>Clamps <paramref name="value"/> into <c>[min, max]</c>.</summary>
    public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);

    /// <summary>True when the range has exactly one valid choice — the input should be read-only.</summary>
    public static bool IsFixed(int min, int max) => min == max;
}
