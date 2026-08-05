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
}
