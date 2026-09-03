namespace Risk.Domain.Missions;

/// <summary>
/// Eliminate the player holding the given <see cref="ArmyId"/>. If that
/// army is the holder's own seat, the card's printed fallback applies
/// instead — occupy 24 territories (reglasrisk.md:84-85; resolved at
/// completion-check time, roadmap 3.3, never substituted at deal time).
/// Carries an explicit <see cref="ArmyId"/> rather than being field-less
/// so the 6 elimination cards stay distinguishable, which is what lets
/// setup remove unseated armies' cards before dealing (roadmap 3.2,
/// reglasrisk.md setup step 2).
/// </summary>
public sealed record EliminateArmy(ArmyId Army) : MissionCard;
