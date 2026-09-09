using Risk.Engine.Combat;

namespace Risk.AI.Scoring;

/// <summary>
/// A precomputed expected-value table over the engine's own
/// <see cref="BattleResolver"/> dice-comparison rule (design decision D6):
/// every one of the 6 possible (attackerDice 1..3, defenderDice 1..2)
/// combinations is enumerated exhaustively — at most 6^5 = 7776 roll
/// tuples — once, in this static initializer, so <see cref="ExpectedNetTroopChange"/>
/// is an O(1) lookup at decision time. This is single-round expectation
/// only: no multi-round simulation, no rollout, no search of any kind.
/// </summary>
internal static class CombatOdds
{
    private const int MaxAttackerDice = 3;
    private const int MaxDefenderDice = 2;

    private static readonly double[,] ExpectedAttackerLossesTable = new double[MaxAttackerDice + 1, MaxDefenderDice + 1];
    private static readonly double[,] ExpectedDefenderLossesTable = new double[MaxAttackerDice + 1, MaxDefenderDice + 1];

    static CombatOdds()
    {
        for (var attackerDice = 1; attackerDice <= MaxAttackerDice; attackerDice++)
        {
            for (var defenderDice = 1; defenderDice <= MaxDefenderDice; defenderDice++)
            {
                var (attackerLosses, defenderLosses) = ComputeExpectedLosses(attackerDice, defenderDice);
                ExpectedAttackerLossesTable[attackerDice, defenderDice] = attackerLosses;
                ExpectedDefenderLossesTable[attackerDice, defenderDice] = defenderLosses;
            }
        }
    }

    /// <summary>Dice rolled by an attacker with <paramref name="fromTroops"/> in the origin territory.</summary>
    public static int AttackerDice(int fromTroops) => Math.Clamp(fromTroops - 1, 0, MaxAttackerDice);

    /// <summary>Dice rolled by a defender with <paramref name="defenderTroops"/> in the target territory.</summary>
    public static int DefenderDice(int defenderTroops) => Math.Clamp(defenderTroops, 0, MaxDefenderDice);

    public static double ExpectedAttackerLosses(int attackerDice, int defenderDice) =>
        ExpectedAttackerLossesTable[attackerDice, defenderDice];

    public static double ExpectedDefenderLosses(int attackerDice, int defenderDice) =>
        ExpectedDefenderLossesTable[attackerDice, defenderDice];

    /// <summary>Expected defender losses minus expected attacker losses for one battle round.</summary>
    public static double ExpectedNetTroopChange(int attackerDice, int defenderDice) =>
        ExpectedDefenderLosses(attackerDice, defenderDice) - ExpectedAttackerLosses(attackerDice, defenderDice);

    private static (double AttackerLosses, double DefenderLosses) ComputeExpectedLosses(int attackerDice, int defenderDice)
    {
        long totalOutcomes = 0;
        long totalAttackerLosses = 0;
        long totalDefenderLosses = 0;

        foreach (var attackerRolls in AllRollCombinations(attackerDice))
        {
            foreach (var defenderRolls in AllRollCombinations(defenderDice))
            {
                var outcome = BattleResolver.Resolve(attackerRolls, defenderRolls);
                totalAttackerLosses += outcome.AttackerLosses;
                totalDefenderLosses += outcome.DefenderLosses;
                totalOutcomes++;
            }
        }

        return ((double)totalAttackerLosses / totalOutcomes, (double)totalDefenderLosses / totalOutcomes);
    }

    private static IEnumerable<int[]> AllRollCombinations(int diceCount)
    {
        var rolls = new int[diceCount];
        return Generate(0, rolls);

        static IEnumerable<int[]> Generate(int index, int[] rolls)
        {
            if (index == rolls.Length)
            {
                yield return (int[])rolls.Clone();
                yield break;
            }

            for (var value = 1; value <= 6; value++)
            {
                rolls[index] = value;

                foreach (var result in Generate(index + 1, rolls))
                {
                    yield return result;
                }
            }
        }
    }
}
