namespace Risk.AI.Scoring;

/// <summary>
/// Every heuristic constant used across the bot's scoring and decision
/// classes (design decision D10). Scoring/decision code should contain no
/// numeric literals other than structural ones (0, 1, array indices).
/// Values below are the initial, deliberately conservative estimates called
/// for by the design; several are explicitly flagged in the design's Open
/// Questions as subject to tuning once the Phase 9 full-game integration
/// tests expose actual termination/aggression behavior. Centralizing them
/// here (rather than scattering literals across six files) is what makes
/// that later tuning pass, or a future difficulty tier, a one-file change.
/// </summary>
internal static class BotWeights
{
    // Claim-phase territory scoring (design: Decision Algorithms / Claim).
    public const double ClaimContinentScarcityWeight = 2.0;
    public const double ClaimAdjacencyWeight = 1.0;
    public const double ClaimHostileNeighborPenalty = 1.5;

    // Shared across Claim/Setup/Headquarters/Reinforce/Attack scoring.
    public const double ContinentBonusWeight = 1.0;

    // SelectHeadquarters-phase scoring (design: Decision Algorithms / SelectHeadquarters).
    public const double HeadquartersHostileNeighborPenalty = 3.0;
    public const double HeadquartersFriendlyNeighborBonus = 2.0;
    public const double HeadquartersTroopsWeight = 0.5;

    // Reinforce-phase placement scoring.
    public const double DefenseUrgencyWeight = 1.0;
    public const double AttackLaunchValue = 0.5;

    // Attack-phase candidate scoring.
    public const double ExpectedNetTroopWeight = 1.0;
    public const double SourceExposurePenalty = 0.75;
    public const double NeutralTargetPenalty = 2.0;
    public const double ObjectiveOverrideThreshold = 3.0;
    public const double EliminationBonus = 5.0;

    // Shared across Attack/Fortify candidate generation: the engine requires a territory to
    // keep at least 1 troop behind for both AttackCommand and FortifyCommand, so a territory
    // needs at least 2 troops to have any troop free to send.
    public const int MinimumSourceTroopsToAct = 2;

    // Fortify-phase scoring.
    public const double FortifyMinimumGain = 1.0;
    // Bounds how many of a component's most-urgent frontier targets are evaluated per
    // candidate source, so widening the search to every (from, to) pair within a component
    // (post-review-reliability fix: a single fixed "safest source" reservoir was missing
    // better fortifies from higher-troop, slightly-less-safe sources) stays cheap even
    // though Risk's map is small (42 territories, at most a handful of components).
    public const int FortifyFrontierCandidateLimit = 5;

    // Mission scoring (bot-objective-awareness).
    public const double MissionOccupyWeight = 2.0;
    public const double MissionTopUpWeight = 2.0;
    public const double MissionContinentWeight = 2.0;
    public const double MissionEliminateWeight = 2.0;
    // The minimum OccupyTerritories.MinArmiesPerTerritory at which topping up a territory's
    // garrison is a distinct mission event: every occupied territory already carries at
    // least 1 troop, so a threshold of 1 could never be "crossed" by a placement. Shared by
    // MissionScoring (GainForReinforcing) and ReinforceDecision (top-up mode trigger) so the
    // two never drift out of sync.
    public const int MinArmiesRequiringTopUp = 2;

    // Capital scoring.
    public const double EnemyHqCaptureWeight = 3.0;
    public const double HqDefenseWeight = 3.0;
    // Deliberately higher than EnemyHqCaptureWeight's maximum possible value
    // (EnemyHqCaptureWeight / 1, the weakest-garrison case): CapitalVictoryRule makes
    // holding your OWN headquarters a hard precondition for winning, so recapturing it
    // once lost must always outrank hunting any enemy headquarters, regardless of tuning.
    public const double RecaptureOwnHqWeight = 10.0;

    // BotTurnRunner safety valves (design D5 / Open Questions).
    public const int MaxCommandsPerTurn = 1_000;
    public const int MaxCommandsPerGame = 250_000;
}
