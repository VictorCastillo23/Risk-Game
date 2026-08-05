using Risk.Domain.Dice;

namespace Risk.Web.Services;

/// <summary>
/// Production <see cref="IDiceRoller"/> backed by <see cref="Random.Shared"/>.
/// The engine takes the roller by constructor injection so combat stays
/// testable; this is the one implementation that provides real randomness,
/// owned by the composition root rather than by Risk.Engine/Risk.Domain.
/// </summary>
public sealed class RandomDiceRoller : IDiceRoller
{
    public IReadOnlyList<int> Roll(int count)
    {
        var rolls = new int[count];
        for (var i = 0; i < count; i++)
        {
            rolls[i] = Random.Shared.Next(1, 7);
        }

        return rolls;
    }
}
