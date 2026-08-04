namespace Risk.Domain.Dice;

/// <summary>
/// Abstraction over dice rolling so combat resolution can be driven
/// deterministically in tests via a fake implementation, while production
/// code uses a genuinely random roller.
/// </summary>
public interface IDiceRoller
{
    /// <summary>
    /// Rolls the requested number of six-sided dice for a single battle round.
    /// </summary>
    /// <param name="count">Number of dice to roll (1-3 for an attacker, 1-2 for a defender).</param>
    /// <returns>The rolled values, one per die, in the order rolled.</returns>
    IReadOnlyList<int> Roll(int count);
}
