using Risk.Domain.Dice;
using Risk.Domain.Players;

namespace Risk.Engine.Setup;

/// <summary>
/// Determines which player takes the first turn via a one-die-per-player
/// roll with re-roll tie-break, mirroring classic Risk's opening ritual.
/// Mode-agnostic: TwoPlayer simply never calls this.
/// </summary>
public static class TurnOrder
{
    /// <summary>
    /// Each player rolls one die; the unique highest goes first. On a tie,
    /// only the tied players re-roll, repeating until one is highest.
    /// </summary>
    /// <param name="players">The candidates for the first turn.</param>
    /// <param name="dice">
    /// The dice source. Reuses <see cref="IDiceRoller"/> for setup even
    /// though its XML doc describes rolls "for a single battle round" —
    /// a benign semantic stretch, not an interface change.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="players"/> is empty.</exception>
    public static PlayerId DetermineFirst(IReadOnlyList<PlayerId> players, IDiceRoller dice)
    {
        if (players.Count == 0)
        {
            throw new ArgumentException("At least one player is required.", nameof(players));
        }

        var candidates = players;

        while (true)
        {
            var winners = new List<PlayerId>();
            var maxRoll = 0;

            foreach (var candidate in candidates)
            {
                var roll = dice.Roll(1)[0];

                if (roll > maxRoll)
                {
                    maxRoll = roll;
                    winners.Clear();
                    winners.Add(candidate);
                }
                else if (roll == maxRoll)
                {
                    winners.Add(candidate);
                }
            }

            if (winners.Count == 1)
            {
                return winners[0];
            }

            candidates = winners;
        }
    }
}
