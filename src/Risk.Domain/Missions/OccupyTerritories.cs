namespace Risk.Domain.Missions;

/// <summary>
/// Occupy at least <paramref name="Count"/> territories, each with at least
/// <paramref name="MinArmiesPerTerritory"/> armies.
/// </summary>
public sealed record OccupyTerritories(int Count, int MinArmiesPerTerritory = 1) : MissionCard;
