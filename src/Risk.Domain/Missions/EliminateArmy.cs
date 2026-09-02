namespace Risk.Domain.Missions;

/// <summary>
/// Eliminate the player holding the given <see cref="ArmyId"/> (or, if
/// that army is unseated, occupy every territory it would have owned —
/// roadmap 3.3's completion-checking concern). Carries an explicit
/// <see cref="ArmyId"/> field rather than being field-less so the 6
/// elimination cards remain distinguishable, e.g. for removing the
/// unused-color card before dealing.
/// </summary>
public sealed record EliminateArmy(ArmyId Army) : MissionCard;
