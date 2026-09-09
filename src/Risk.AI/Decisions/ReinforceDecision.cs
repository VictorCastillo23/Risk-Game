using Risk.AI.Scoring;
using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Rules;
using Risk.Engine.Views;

namespace Risk.AI.Decisions;

/// <summary>
/// Reinforce-phase decision (design's Decision Algorithms / Reinforce). Ordering is
/// load-bearing: seed the pool, then trade eagerly (before any placement — trading
/// after placing would leave the tracked pool non-zero and <see cref="EndPhaseCommand"/>
/// would be rejected), then either end the phase or place.
/// </summary>
internal static class ReinforceDecision
{
    public static (GameCommand Command, BotMemory Memory) Decide(PlayerView view, PlayerId self, BotMemory memory)
    {
        var seededMemory = SeedPool(view, self, memory);
        var pool = seededMemory.ReinforcePool ?? 0;

        if (CardTradeSelection.BestTrade(view, self) is { } trade)
        {
            return (trade, seededMemory);
        }

        if (pool <= 0)
        {
            return (new EndPhaseCommand(self), seededMemory);
        }

        var (target, troops) = SelectPlacement(view, self, pool);
        return (new PlaceTroopsCommand(self, target, troops), seededMemory);
    }

    /// <summary>
    /// Adds the base allotment (<see cref="Reinforcement.Calculate"/>) to
    /// <see cref="BotMemory.ReinforcePool"/> exactly once per visit (binding seeding
    /// contract on <see cref="BotMemory.ReinforcePoolSeeded"/>, Phase 4). Never coalesces
    /// with <c>??</c>: a non-null, not-yet-seeded pool may be nothing but an early-banked
    /// trade bonus from a mandatory turn-start trade-down, and that value must be ADDED to,
    /// not replaced.
    /// </summary>
    private static BotMemory SeedPool(PlayerView view, PlayerId self, BotMemory memory)
    {
        if (memory.ReinforcePoolSeeded)
        {
            return memory;
        }

        var baseAllotment = Reinforcement.Calculate(view.Territories, self);
        var pool = (memory.ReinforcePool ?? 0) + baseAllotment;

        return memory with { ReinforcePool = pool, ReinforcePoolSeeded = true };
    }

    private static (TerritoryId Territory, int Troops) SelectPlacement(PlayerView view, PlayerId self, int pool)
    {
        if (TryTopUp(view, self, pool, out var topUp))
        {
            return topUp;
        }

        var frontier = new HashSet<TerritoryId>(TerritoryScoring.Frontier(view, self));

        var best = TerritoryScoring.Owned(view, self)
            .OrderByDescending(id => PlacementScore(view, self, id, pool, frontier.Contains(id)))
            .ThenBy(TerritoryScoring.IndexOf)
            .First();

        return (best, pool);
    }

    private static bool TryTopUp(PlayerView view, PlayerId self, int pool, out (TerritoryId Territory, int Troops) result)
    {
        result = default;

        if (view.OwnEffectiveMission is not OccupyTerritories(_, var minArmies) || minArmies < BotWeights.MinArmiesRequiringTopUp)
        {
            return false;
        }

        var target = TerritoryScoring.Owned(view, self)
            .Where(id => view.Territories[id].Troops < minArmies)
            .OrderByDescending(id => TerritoryScoring.DefenseUrgency(TerritoryScoring.Facts(view, self, id)))
            .ThenBy(TerritoryScoring.IndexOf)
            .Cast<TerritoryId?>()
            .FirstOrDefault();

        if (target is not { } territory)
        {
            return false;
        }

        var needed = minArmies - view.Territories[territory].Troops;
        result = (territory, Math.Min(pool, needed));
        return true;
    }

    /// <summary>
    /// <c>DefenseUrgencyWeight×DefenseUrgency + ContinentBonusWeight×ContinentPressure +
    /// AttackLaunchValue + MissionScoring.GainForReinforcing + CapitalScoring.GainForDefending</c>
    /// (design's Reinforce placement formula). The generic (first three) terms are zeroed for
    /// any non-frontier territory — mission/capital scoring may still lift it.
    /// </summary>
    private static double PlacementScore(PlayerView view, PlayerId self, TerritoryId id, int pool, bool isFrontier)
    {
        var generic = 0.0;

        if (isFrontier)
        {
            var facts = TerritoryScoring.Facts(view, self, id);
            generic = BotWeights.DefenseUrgencyWeight * TerritoryScoring.DefenseUrgency(facts)
                + BotWeights.ContinentBonusWeight * TerritoryScoring.ContinentPressure(view, self, id)
                + BotWeights.AttackLaunchValue;
        }

        return generic
            + MissionScoring.GainForReinforcing(view, self, id, pool)
            + CapitalScoring.GainForDefending(view, self, id);
    }
}
