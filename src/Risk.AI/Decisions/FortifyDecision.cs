using Risk.AI.Scoring;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Rules;
using Risk.Engine.Views;

namespace Risk.AI.Decisions;

/// <summary>
/// Fortify-phase decision (design's Decision Algorithms / Fortify). At most
/// once per turn (<see cref="Risk.Engine.State.TurnState.FortifyUsed"/>),
/// only along a friendly path the same way the engine itself enforces
/// (<see cref="ConnectivityRules.HasFriendlyPath"/>), and only when it
/// produces a measurable improvement — never a pointless move.
/// </summary>
internal static class FortifyDecision
{
    private readonly record struct Candidate(TerritoryId From, TerritoryId To, int Troops, double NetGain);

    public static GameCommand Decide(PlayerView view, PlayerId self)
    {
        if (view.Turn.FortifyUsed)
        {
            return new EndPhaseCommand(self);
        }

        var best = BestCandidate(view, self);

        if (best is not { } candidate ||
            candidate.NetGain < BotWeights.FortifyMinimumGain ||
            !ConnectivityRules.HasFriendlyPath(view.Territories, self, candidate.From, candidate.To))
        {
            return new EndPhaseCommand(self);
        }

        return new FortifyCommand(self, candidate.From, candidate.To, candidate.Troops);
    }

    private static Candidate? BestCandidate(PlayerView view, PlayerId self)
    {
        Candidate? best = null;

        foreach (var component in TerritoryScoring.OwnComponents(view, self))
        {
            foreach (var candidate in CandidatesWithin(view, self, component))
            {
                if (IsBetter(candidate, best))
                {
                    best = candidate;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// True if <paramref name="candidate"/> beats <paramref name="current"/>: higher net
    /// gain, or a tie broken by the lower <see cref="TerritoryScoring.IndexOf"/> for
    /// <c>From</c> then <c>To</c> (design's shared tie-break order).
    /// </summary>
    private static bool IsBetter(Candidate candidate, Candidate? current)
    {
        if (current is not { } existing)
        {
            return true;
        }

        if (candidate.NetGain != existing.NetGain)
        {
            return candidate.NetGain > existing.NetGain;
        }

        var fromComparison = TerritoryScoring.IndexOf(candidate.From).CompareTo(TerritoryScoring.IndexOf(existing.From));
        return fromComparison != 0
            ? fromComparison < 0
            : TerritoryScoring.IndexOf(candidate.To) < TerritoryScoring.IndexOf(existing.To);
    }

    private static IEnumerable<Candidate> CandidatesWithin(PlayerView view, PlayerId self, IReadOnlySet<TerritoryId> component)
    {
        var fromCandidates = component.Where(id => view.Territories[id].Troops >= 2).ToList();
        if (fromCandidates.Count == 0)
        {
            yield break;
        }

        var from = fromCandidates
            .OrderBy(id => TerritoryScoring.DefenseUrgency(TerritoryScoring.Facts(view, self, id)))
            .ThenBy(TerritoryScoring.IndexOf)
            .First();

        var frontierTargets = component
            .Where(id => !id.Equals(from) && TerritoryScoring.Facts(view, self, id).HostileNeighbors > 0)
            .OrderByDescending(id => TerritoryScoring.DefenseUrgency(TerritoryScoring.Facts(view, self, id)))
            .ThenBy(TerritoryScoring.IndexOf)
            .Take(5);

        var fromTroops = view.Territories[from].Troops;

        foreach (var to in frontierTargets)
        {
            var facts = TerritoryScoring.Facts(view, self, to);
            var urgencyBefore = TerritoryScoring.DefenseUrgency(facts);
            var troops = Math.Min(fromTroops - 1, Math.Max(1, urgencyBefore));

            var toTroopsAfter = view.Territories[to].Troops + troops;
            var urgencyAfter = Math.Max(0, facts.HostileNeighborTroops - toTroopsAfter);
            var netGain = urgencyBefore - urgencyAfter;

            yield return new Candidate(from, to, troops, netGain);
        }
    }
}
