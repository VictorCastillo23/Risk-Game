namespace Risk.Domain.Missions;

/// <summary>
/// A secret mission card: one of exactly three archetypes
/// (<see cref="OccupyTerritories"/>, <see cref="EliminateArmy"/>,
/// <see cref="ConquerContinents"/>). The constructor is
/// <c>private protected</c> so the hierarchy is genuinely closed to
/// external assemblies, unlike <see cref="Cards.Card"/>'s implicit
/// protected constructor.
/// </summary>
public abstract record MissionCard
{
    private protected MissionCard()
    {
    }
}
