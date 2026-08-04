using Risk.Engine.Combat;

namespace Risk.Tests.Engine;

public class BattleResolverTests
{
    [Fact]
    public void Resolve_a_tied_pair_favors_the_defender()
    {
        // Spec scenario "Tie favors defender": attacker [5,3] vs defender [5,2].
        // The tied top pair (5 vs 5) goes to the defender; the second pair
        // (3 vs 2) goes to the attacker.
        var outcome = BattleResolver.Resolve([5, 3], [5, 2]);

        Assert.Equal(1, outcome.AttackerLosses);
        Assert.Equal(1, outcome.DefenderLosses);
    }

    [Fact]
    public void Resolve_awards_the_pair_to_whichever_side_rolled_strictly_higher()
    {
        var outcome = BattleResolver.Resolve([6, 6], [1, 1]);

        Assert.Equal(0, outcome.AttackerLosses);
        Assert.Equal(2, outcome.DefenderLosses);
    }

    [Fact]
    public void Resolve_only_compares_the_minimum_number_of_pairs_when_dice_counts_differ()
    {
        // Attacker rolled 3 dice, defender only 1 (e.g. defending territory
        // has just 1 troop): only the top pair is compared, the attacker's
        // extra dice are irrelevant.
        var outcome = BattleResolver.Resolve([6, 5, 4], [3]);

        Assert.Equal(0, outcome.AttackerLosses);
        Assert.Equal(1, outcome.DefenderLosses);
    }

    [Fact]
    public void Resolve_sorts_each_sides_rolls_highest_first_before_comparing()
    {
        // Unsorted input: attacker's real highest is 2 (not the first
        // element), defender's real highest is 6. Real GREEN requires the
        // production code to sort before comparing, not just zip in order.
        var outcome = BattleResolver.Resolve([1, 2], [6, 1]);

        Assert.Equal(new[] { 2, 1 }, outcome.AttackerRolls);
        Assert.Equal(new[] { 6, 1 }, outcome.DefenderRolls);
        // Sorted: attacker [2,1] vs defender [6,1] -> pair 1 (2 vs 6) defender
        // wins, pair 2 (1 vs 1) tie favors defender -> attacker loses both.
        Assert.Equal(2, outcome.AttackerLosses);
        Assert.Equal(0, outcome.DefenderLosses);
    }
}
