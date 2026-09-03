using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class AttackDiceOptionsTests
{
    // Mirrors the engine's own rule (GameEngine.ExecuteAttack): DiceCount must
    // be 1-3 and no more than attackerTerritory.Troops - 1 (one troop must
    // always remain behind).

    [Theory]
    [InlineData(1, 0)]  // only 1 troop: can't attack at all, 0 remains after 1 stays behind
    [InlineData(2, 1)]  // 1 troop can leave
    [InlineData(4, 3)]  // 3 troops can leave, capped at the 3-dice rule ceiling
    [InlineData(10, 3)] // way more troops than 3: still capped at 3
    public void MaxDice_ReturnsAttackerTroopsMinusOne_CappedAtThree(int attackerTroops, int expectedMax)
    {
        Assert.Equal(expectedMax, AttackDiceOptions.MaxDice(attackerTroops));
    }

    [Fact]
    public void AvailableDiceCounts_WithOneTroop_IsEmpty()
    {
        Assert.Empty(AttackDiceOptions.AvailableDiceCounts(1));
    }

    [Fact]
    public void AvailableDiceCounts_WithTwoTroops_OffersOnlyOneDie()
    {
        Assert.Equal(new[] { 1 }, AttackDiceOptions.AvailableDiceCounts(2));
    }

    [Fact]
    public void AvailableDiceCounts_WithFourTroops_OffersOneThroughThree()
    {
        Assert.Equal(new[] { 1, 2, 3 }, AttackDiceOptions.AvailableDiceCounts(4));
    }

    // A freshly-selected attack (new origin/destination pair) should default
    // to the maximum legal dice count, not 1 — the player can still lower it
    // manually afterward, but the starting point is the strongest legal attack.

    [Theory]
    [InlineData(4, 3)]  // plenty of troops: default starts at the 3-dice ceiling
    [InlineData(3, 2)]  // exactly enough for 2 dice
    [InlineData(2, 1)]  // only 2 troops total: default starts at the only legal option, 1
    public void DefaultDiceCount_IsTheMaximumLegalValue(int attackerTroops, int expectedDefault)
    {
        Assert.Equal(expectedDefault, AttackDiceOptions.DefaultDiceCount(attackerTroops));
    }
}
