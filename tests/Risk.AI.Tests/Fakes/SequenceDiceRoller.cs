using Risk.Domain.Dice;

namespace Risk.AI.Tests.Fakes;

/// <summary>
/// A deterministic <see cref="IDiceRoller"/> over a fixed, cycling sequence
/// of die values. Unlike <c>QueuedDiceRoller</c> (which throws once its
/// queue is exhausted), this roller never runs out: once the sequence is
/// consumed it wraps back to the start, so it can drive an entire bot-vs-bot
/// game of unknown length. The default sequence has a prime length (17) so
/// its cycle never phases in lockstep with the fixed 1/2/3-dice roll sizes
/// used throughout combat.
/// </summary>
internal sealed class SequenceDiceRoller : IDiceRoller
{
    private static readonly int[] DefaultSequence =
    [
        6, 5, 4, 3, 2, 1, 6, 4, 2, 5, 3, 1, 6, 6, 1, 4, 3
    ];

    private readonly IReadOnlyList<int> _sequence;
    private int _index;

    public SequenceDiceRoller(IReadOnlyList<int>? sequence = null)
    {
        _sequence = sequence ?? DefaultSequence;

        if (_sequence.Count == 0)
        {
            throw new ArgumentException("SequenceDiceRoller: sequence must not be empty.", nameof(sequence));
        }
    }

    /// <summary>Number of values in the underlying cycling sequence.</summary>
    public int SequenceLength => _sequence.Count;

    public IReadOnlyList<int> Roll(int count)
    {
        var rolls = new int[count];

        for (var i = 0; i < count; i++)
        {
            rolls[i] = _sequence[_index];
            _index = (_index + 1) % _sequence.Count;
        }

        return rolls;
    }
}
