using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Views;

namespace Risk.AI.Scoring;

/// <summary>
/// Capital-mode-aware strategic weighting, reading ONLY
/// <see cref="PlayerView.OwnHeadquarters"/> and <see cref="PlayerView.RevealedHeadquarters"/>
/// (design bot-objective-awareness). Returns <c>0.0</c> outside Capital mode — including the
/// pre-selection window where <see cref="PlayerView.OwnHeadquarters"/> is still
/// <see langword="null"/> — so generic scoring is unaffected there.
/// </summary>
internal static class CapitalScoring
{
    /// <summary>
    /// Strategic value of attacking <paramref name="target"/> once every player's
    /// headquarters is revealed: scores the enemy headquarters among
    /// <see cref="PlayerView.RevealedHeadquarters"/> inversely by its garrison, so the
    /// weakest-defended enemy HQ receives the highest weight. Zero before full reveal,
    /// zero for any territory that is not a revealed enemy headquarters.
    /// </summary>
    public static double GainForCapturing(PlayerView view, PlayerId self, TerritoryId target)
    {
        if (view.OwnHeadquarters is null || view.RevealedHeadquarters.Count == 0)
        {
            return 0.0;
        }

        var isEnemyHeadquarters = view.RevealedHeadquarters
            .Any(kv => kv.Key != self && kv.Value.Equals(target));

        if (!isEnemyHeadquarters)
        {
            return 0.0;
        }

        var troops = view.Territories.TryGetValue(target, out var state) ? Math.Max(1, state.Troops) : 1;

        return BotWeights.EnemyHqCaptureWeight / troops;
    }

    /// <summary>
    /// Strategic value of reinforcing <paramref name="own"/> to defend the bot's own
    /// headquarters before all headquarters are revealed: raises the weight of
    /// <see cref="PlayerView.OwnHeadquarters"/> exactly when it is threatened
    /// (<see cref="TerritoryScoring.DefenseUrgency"/> above zero). Zero outside Capital
    /// mode, zero for any territory other than the bot's own headquarters.
    /// </summary>
    public static double GainForDefending(PlayerView view, PlayerId self, TerritoryId own)
    {
        if (view.OwnHeadquarters is not { } headquarters || !own.Equals(headquarters))
        {
            return 0.0;
        }

        var facts = TerritoryScoring.Facts(view, self, own);

        return TerritoryScoring.DefenseUrgency(facts) > 0 ? BotWeights.HqDefenseWeight : 0.0;
    }
}
