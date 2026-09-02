namespace Risk.Domain.Missions;

/// <summary>
/// Identifies one of the physical armies in the box (color), not a seat.
/// A seat's <c>ArmyId</c>-&gt;<see cref="Players.PlayerId"/> binding happens
/// at deal time (roadmap 3.2), never here.
/// </summary>
public readonly record struct ArmyId(int Value);
