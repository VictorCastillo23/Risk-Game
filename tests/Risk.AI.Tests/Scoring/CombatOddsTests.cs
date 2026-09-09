using Risk.AI.Scoring;

namespace Risk.AI.Tests.Scoring;

public class CombatOddsTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(4, 3)]
    [InlineData(10, 3)]
    public void AttackerDice_clamps_between_zero_and_three(int fromTroops, int expectedDice)
    {
        Assert.Equal(expectedDice, CombatOdds.AttackerDice(fromTroops));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(5, 2)]
    public void DefenderDice_clamps_at_two(int defenderTroops, int expectedDice)
    {
        Assert.Equal(expectedDice, CombatOdds.DefenderDice(defenderTroops));
    }

    [Fact]
    public void ExpectedNetTroopChange_favors_the_attacker_at_three_dice_versus_two()
    {
        // Standard published Risk 3-vs-2 dice odds over 6^5 = 7776 equally
        // likely outcomes: defender loses both 2890, split 1-1 2611,
        // attacker loses both 2275. Net = ((2890*2+2611) - (2611+2275*2)) / 7776.
        var expected = 1230.0 / 7776.0;

        Assert.Equal(expected, CombatOdds.ExpectedNetTroopChange(3, 2), precision: 4);
    }

    [Fact]
    public void ExpectedNetTroopChange_favors_the_defender_at_two_dice_versus_two()
    {
        // Standard published Risk 2-vs-2 dice odds over 6^4 = 1296 outcomes:
        // defender loses both 295, split 1-1 420, attacker loses both 581.
        var expected = -572.0 / 1296.0;

        Assert.Equal(expected, CombatOdds.ExpectedNetTroopChange(2, 2), precision: 4);
    }

    [Fact]
    public void ExpectedNetTroopChange_favors_the_defender_at_one_die_versus_one_due_to_the_tie_rule()
    {
        // 1v1 over 36 outcomes: attacker wins 15, defender wins-or-ties 21
        // (ties favor the defender). Net = (15 - 21) / 36.
        var expected = -6.0 / 36.0;

        Assert.Equal(expected, CombatOdds.ExpectedNetTroopChange(1, 1), precision: 4);
    }
}
