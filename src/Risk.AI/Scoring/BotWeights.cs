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

    // Reinforce-phase placement scoring.
    public const double DefenseUrgencyWeight = 1.0;
    public const double AttackLaunchValue = 0.5;

    // Attack-phase candidate scoring.
    public const double ExpectedNetTroopWeight = 1.0;
    public const double SourceExposurePenalty = 0.75;
    public const double NeutralTargetPenalty = 2.0;
    public const double ObjectiveOverrideThreshold = 3.0;
    public const double EliminationBonus = 5.0;

    // Fortify-phase scoring.
    public const double FortifyMinimumGain = 1.0;

    // Mission scoring (bot-objective-awareness).
    public const double MissionOccupyWeight = 2.0;
    public const double MissionTopUpWeight = 2.0;
    public const double MissionContinentWeight = 2.0;
    public const double MissionEliminateWeight = 2.0;

    // Capital scoring.
    public const double EnemyHqCaptureWeight = 3.0;
    public const double HqDefenseWeight = 3.0;

    // BotTurnRunner safety valves (design D5 / Open Questions).
    public const int MaxCommandsPerTurn = 1_000;
    public const int MaxCommandsPerGame = 250_000;
}
