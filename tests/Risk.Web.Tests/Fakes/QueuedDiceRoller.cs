using Risk.Domain.Dice;

namespace Risk.Web.Tests.Fakes;

/// <summary>
/// A deterministic <see cref="IDiceRoller"/> driven by a pre-loaded queue of
/// rolls. Mirrors <c>Risk.Tests.Fakes.QueuedDiceRoller</c> exactly (that
/// type is <c>internal</c> to <c>Risk.Tests</c>, a separate assembly with no
/// <c>InternalsVisibleTo</c>, so it cannot be shared directly) — same shape,
/// same codebase convention as <see cref="AlwaysAttackerWinsDiceRoller"/>
/// (<c>Risk.Web.Tests/Fakes/</c> mirrors <c>Risk.Tests/Fakes/</c>).
/// </summary>
internal sealed class QueuedDiceRoller : IDiceRoller
{
    private readonly Queue<IReadOnlyList<int>> _rolls = new();

    /// <summary>
    /// Builds a <see cref="QueuedDiceRoller"/> pre-loaded with a tie-free,
    /// strictly descending roll-off sequence (6, 5, 4, 3, 2, ...) — one
    /// single-die roll per candidate — so <c>TurnOrder.DetermineFirst</c>
    /// always resolves in exactly one round with player 0 winning. Supports
    /// at most 5 players (Risk's max legal count).
    /// </summary>
    public static QueuedDiceRoller ForRollOff(int playerCount)
    {
        var roller = new QueuedDiceRoller();
        var values = new[] { 6, 5, 4, 3, 2 };

        for (var i = 0; i < playerCount; i++)
        {
            roller.Enqueue(values[i]);
        }

        return roller;
    }

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
