namespace Risk.Engine.Combat;

/// <summary>
/// The result of comparing one battle round's attacker and defender dice:
/// the rolls themselves (sorted highest-first) and how many troops each side
/// lost.
/// </summary>
public sealed record BattleOutcome(
    IReadOnlyList<int> AttackerRolls,
    IReadOnlyList<int> DefenderRolls,
    int AttackerLosses,
    int DefenderLosses);
