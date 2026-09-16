namespace Risk.Engine.Rules;

/// <summary>
/// The classic Risk card trade-in bonus scale: 4, 6, 8, 10, 12, 15 troops
/// for the first six trades this game, then +5 troops for every trade after
/// that (pinned in the proposal).
/// </summary>
public static class CardTradeBonus
{
    /// <summary>
    /// A hand at or above this size forces a trade before any other command
    /// (see <see cref="GameEngine"/>'s mandatory-trade gate). The single
    /// owner of this rule value — <c>Risk.AI.BotPlayer</c> and
    /// <c>CardPanel.razor</c> both reference this instead of keeping their
    /// own copy.
    /// </summary>
    public const int MandatoryHandThreshold = 5;

    private static readonly int[] FixedScale = [4, 6, 8, 10, 12, 15];
    private const int EscalationStep = 5;

    /// <param name="tradeNumber">1-based count of this trade (1 = the first trade this game).</param>
    public static int ForTradeNumber(int tradeNumber)
    {
        if (tradeNumber <= FixedScale.Length)
        {
            return FixedScale[tradeNumber - 1];
        }

        return FixedScale[^1] + EscalationStep * (tradeNumber - FixedScale.Length);
    }
}
