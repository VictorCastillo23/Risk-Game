using Risk.AI.Scoring;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Views;

namespace Risk.AI.Decisions;

/// <summary>
/// Capital-mode SelectHeadquarters-phase selection (design's Decision
/// Algorithms / SelectHeadquarters). Ownership is the engine's only
/// constraint, so every owned territory is a candidate; prefers interior
/// (few enemy neighbors), well-connected (many friendly neighbors)
/// territory within the most valuable continent the bot already controls.
/// </summary>
internal static class HeadquartersDecision
{
    public static GameCommand Decide(PlayerView view, PlayerId self)
    {
        var best = TerritoryScoring.Owned(view, self)
            .OrderByDescending(id => Score(view, self, id))
            .ThenBy(TerritoryScoring.IndexOf)
            .First();

        return new SelectHeadquartersCommand(self, best);
    }

    /// <summary>
    /// <c>−HeadquartersHostileNeighborPenalty×hostileNeighbors +
    /// HeadquartersFriendlyNeighborBonus×friendlyNeighbors +
    /// ContinentBonusWeight×ContinentPressure + HeadquartersTroopsWeight×troops</c>
    /// (design's SelectHeadquarters formula). Reuses
    /// <see cref="TerritoryScoring.ContinentPressure"/> for the continent-value term rather
    /// than re-deriving it: every headquarters candidate is already self-owned, so
    /// <c>ContinentPressure</c>'s <c>(ownedOtherMembers + 1) / size</c> is exactly this
    /// territory's owned-fraction-including-itself.
    /// </summary>
    private static double Score(PlayerView view, PlayerId self, TerritoryId id)
    {
        var facts = TerritoryScoring.Facts(view, self, id);

        return -BotWeights.HeadquartersHostileNeighborPenalty * facts.HostileNeighbors
            + BotWeights.HeadquartersFriendlyNeighborBonus * facts.FriendlyNeighbors
            + BotWeights.ContinentBonusWeight * TerritoryScoring.ContinentPressure(view, self, id)
            + BotWeights.HeadquartersTroopsWeight * facts.Troops;
    }
}
