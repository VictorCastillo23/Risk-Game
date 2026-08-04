namespace Risk.Engine.Combat;

/// <summary>
/// Pure dice-comparison logic for one battle round: sorts each side's rolls
/// highest-first, compares pair-by-pair (attacker's highest vs defender's
/// highest, then next pair if both sides rolled 2+ dice), and counts losses.
/// A tie is a defender win for that pair (the defender loses no troop from
/// it, the attacker does).
/// </summary>
public static class BattleResolver
{
    public static BattleOutcome Resolve(IReadOnlyList<int> attackerRolls, IReadOnlyList<int> defenderRolls)
    {
        var sortedAttacker = attackerRolls.OrderByDescending(roll => roll).ToArray();
        var sortedDefender = defenderRolls.OrderByDescending(roll => roll).ToArray();
        var comparedPairs = Math.Min(sortedAttacker.Length, sortedDefender.Length);

        var attackerLosses = 0;
        var defenderLosses = 0;

        for (var i = 0; i < comparedPairs; i++)
        {
            if (sortedAttacker[i] > sortedDefender[i])
            {
                defenderLosses++;
            }
            else
            {
                attackerLosses++;
            }
        }

        return new BattleOutcome(sortedAttacker, sortedDefender, attackerLosses, defenderLosses);
    }
}
