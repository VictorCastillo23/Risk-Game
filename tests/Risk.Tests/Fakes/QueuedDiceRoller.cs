using Risk.Domain.Dice;

namespace Risk.Tests.Fakes;

/// <summary>
/// A deterministic <see cref="IDiceRoller"/> driven by a pre-loaded queue of
/// rolls, so combat tests control exact dice outcomes instead of relying on
/// real randomness. Each call to <see cref="Roll"/> dequeues the next queued
/// roll; it throws if the queue is exhausted or if the queued roll's size
/// doesn't match the requested count, surfacing test setup mistakes
/// immediately instead of silently returning the wrong data.
/// </summary>
internal sealed class QueuedDiceRoller : IDiceRoller
{
    private readonly Queue<IReadOnlyList<int>> _rolls = new();

    public QueuedDiceRoller Enqueue(params int[] values)
    {
        _rolls.Enqueue(values);
        return this;
    }

    public IReadOnlyList<int> Roll(int count)
    {
        if (_rolls.Count == 0)
        {
            throw new InvalidOperationException("QueuedDiceRoller: no more queued rolls.");
        }

        var next = _rolls.Dequeue();
        if (next.Count != count)
        {
            throw new InvalidOperationException(
                $"QueuedDiceRoller: expected a roll of {count} dice but the next queued roll has {next.Count}.");
        }

        return next;
    }
}
