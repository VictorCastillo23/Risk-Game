namespace Risk.Web.Models;

/// <summary>
/// Pure computation of the valid dice-count range for an <c>AttackCommand</c>,
/// mirroring the engine's own rule (<c>GameEngine.ExecuteAttack</c>):
/// between 1 and 3 dice, never more than <c>attackerTerritory.Troops - 1</c>
/// (one troop must always remain behind). Kept here, not re-derived in
/// markup, so <c>AttackPanel</c> stays a thin dispatcher (design's
/// "components never hold rules" principle).
/// </summary>
public static class AttackDiceOptions
{
    private const int MaxAllowedDice = 3;

    /// <summary>The highest dice count a player could pick, given <paramref name="attackerTroops"/> in the attacking territory.</summary>
    public static int MaxDice(int attackerTroops) => Math.Clamp(attackerTroops - 1, 0, MaxAllowedDice);

    /// <summary>Every selectable dice count, low to high. Empty when the attacker has only 1 troop (can't attack at all).</summary>
    public static IReadOnlyList<int> AvailableDiceCounts(int attackerTroops)
    {
        var max = MaxDice(attackerTroops);

        return max < 1 ? [] : Enumerable.Range(1, max).ToList();
    }
}
