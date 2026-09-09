using Risk.AI.Scoring;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Views;

namespace Risk.AI.Decisions;

/// <summary>
/// Attack-phase candidate scoring and dice selection (design's Decision
/// Algorithms / Attack). Never forces a militarily-unfavorable attack unless
/// its objective (mission/capital/elimination) value clears
/// <see cref="BotWeights.ObjectiveOverrideThreshold"/> AND raw troop
/// superiority holds; otherwise ends the phase without attacking.
/// </summary>
internal static class AttackDecision
{
    private readonly record struct Candidate(TerritoryId From, TerritoryId To, int DiceCount, double Ev, double ObjectiveValue, double TotalScore);

    public static GameCommand Decide(PlayerView view, PlayerId self, BotMemory memory)
    {
        var neutral = memory.FindTwoPlayerNeutral(view, self);
        var candidates = BuildCandidates(view, self, neutral);

        // Selection and the accept/reject gate MUST use the same candidate set: filter to
        // only the candidates that would individually pass the gate FIRST, then rank by
        // TotalScore among those survivors. Picking the highest-TotalScore candidate
        // regardless of whether IT passes (and only then gate-checking that one candidate)
        // would let a high-continent/objective-score but combat-unfavorable candidate
        // "win" the ranking and then fail the gate, discarding a genuinely favorable attack
        // elsewhere on the board and forcing an unwarranted EndPhaseCommand.
        var best = candidates
            .Where(c => PassesAcceptGate(view, c))
            .OrderByDescending(c => c.TotalScore)
            .ThenBy(c => TerritoryScoring.IndexOf(c.From))
            .ThenBy(c => TerritoryScoring.IndexOf(c.To))
            .Cast<Candidate?>()
            .FirstOrDefault();

        if (best is not { } candidate)
        {
            return new EndPhaseCommand(self);
        }

        return new AttackCommand(self, candidate.From, candidate.To, candidate.DiceCount);
    }

    /// <summary>
    /// True when <paramref name="candidate"/> is individually worth attacking: positive
    /// combat EV, or an objective value clearing <see cref="BotWeights.ObjectiveOverrideThreshold"/>
    /// with raw troop superiority (<c>fromTroops - 1 &gt;= toTroops</c>) backing it up.
    /// </summary>
    private static bool PassesAcceptGate(PlayerView view, Candidate candidate)
    {
        if (candidate.Ev > 0)
        {
            return true;
        }

        var rawSuperiority = view.Territories[candidate.From].Troops - 1 >= view.Territories[candidate.To].Troops;
        return candidate.ObjectiveValue > BotWeights.ObjectiveOverrideThreshold && rawSuperiority;
    }

    private static IEnumerable<Candidate> BuildCandidates(PlayerView view, PlayerId self, PlayerId? neutral)
    {
        foreach (var from in TerritoryScoring.Owned(view, self))
        {
            var fromTroops = view.Territories[from].Troops;
            if (fromTroops < 2)
            {
                continue;
            }

            foreach (var to in WorldMap.NeighborsOf(from))
            {
                if (!view.Territories.TryGetValue(to, out var toState) || toState.Owner is not { } owner || owner.Equals(self))
                {
                    continue;
                }

                yield return BuildCandidate(view, self, from, fromTroops, to, toState.Troops, owner, neutral);
            }
        }
    }

    private static Candidate BuildCandidate(
        PlayerView view, PlayerId self, TerritoryId from, int fromTroops, TerritoryId to, int toTroops, PlayerId owner, PlayerId? neutral)
    {
        var diceCount = CombatOdds.AttackerDice(fromTroops);
        var defenderDice = CombatOdds.DefenderDice(toTroops);
        var ev = CombatOdds.ExpectedNetTroopChange(diceCount, defenderDice);

        var isLastTerritory = TerritoryScoring.Owned(view, owner).Count == 1;
        var objectiveValue = MissionScoring.GainForCapturing(view, self, to)
            + CapitalScoring.GainForCapturing(view, self, to)
            + (isLastTerritory ? BotWeights.EliminationBonus : 0.0);

        var fromFacts = TerritoryScoring.Facts(view, self, from);
        var expectedRemaining = fromTroops - CombatOdds.ExpectedAttackerLosses(diceCount, defenderDice);
        var postAttackUrgency = Math.Max(0.0, fromFacts.HostileNeighborTroops - expectedRemaining);

        var neutralPenalty = neutral is { } n && owner.Equals(n) ? BotWeights.NeutralTargetPenalty : 0.0;

        var totalScore = BotWeights.ExpectedNetTroopWeight * ev
            + BotWeights.ContinentBonusWeight * TerritoryScoring.ContinentPressure(view, self, to)
            + objectiveValue
            - BotWeights.SourceExposurePenalty * postAttackUrgency
            - neutralPenalty;

        return new Candidate(from, to, diceCount, ev, objectiveValue, totalScore);
    }
}
