using Risk.AI.Scoring;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Views;

namespace Risk.AI.Decisions;

/// <summary>
/// Claim-phase territory selection (design's Decision Algorithms / Claim).
/// Always claims exactly 1 troop on the highest-scoring unclaimed territory
/// — the engine requires exactly one troop per <see cref="ClaimTerritoryCommand"/>.
/// </summary>
internal static class ClaimDecision
{
    public static GameCommand Decide(PlayerView view, PlayerId self)
    {
        var best = Unclaimed(view)
            .OrderByDescending(id => Score(view, self, id))
            .ThenBy(TerritoryScoring.IndexOf)
            .First();

        return new ClaimTerritoryCommand(self, best, 1);
    }

    private static IEnumerable<TerritoryId> Unclaimed(PlayerView view) =>
        view.Territories.Where(kv => kv.Value.Owner is null).Select(kv => kv.Key);

    /// <summary>
    /// <c>ClaimContinentScarcityWeight × continentProgress + ClaimAdjacencyWeight × ownNeighbors
    /// − ClaimHostileNeighborPenalty × hostileNeighbors + ContinentBonusWeight × bonus/size</c>
    /// (design's Claim formula). <c>continentProgress</c> is the fraction of the candidate's
    /// continent <paramref name="self"/> already owns (excluding the candidate itself, since
    /// it is unclaimed) — how close claiming it would come to completing that continent.
    /// </summary>
    private static double Score(PlayerView view, PlayerId self, TerritoryId id)
    {
        var continent = Continents.All.First(c => c.Members.Contains(id));
        var ownedOtherMembers = continent.Members.Count(member =>
            !member.Equals(id) && view.Territories.TryGetValue(member, out var state) && state.Owner == self);
        var continentProgress = (double)ownedOtherMembers / continent.Members.Count;

        // Facts() classifies an unclaimed territory's own owner as null (neither friendly nor
        // hostile per its own contract); since id is itself unclaimed here, that only affects
        // Facts.Owner/Troops, not the FriendlyNeighbors/HostileNeighbors counts we need.
        var facts = TerritoryScoring.Facts(view, self, id);

        return BotWeights.ClaimContinentScarcityWeight * continentProgress
            + BotWeights.ClaimAdjacencyWeight * facts.FriendlyNeighbors
            - BotWeights.ClaimHostileNeighborPenalty * facts.HostileNeighbors
            + BotWeights.ContinentBonusWeight * continent.Bonus / continent.Members.Count;
    }
}
