using Risk.Domain.Map;

namespace Risk.Domain.Missions;

/// <summary>
/// Conquer every continent in <paramref name="Required"/>, plus any
/// <paramref name="WildcardCount"/> additional continents of the holder's
/// choice.
/// </summary>
public sealed record ConquerContinents(IReadOnlyList<ContinentId> Required, int WildcardCount = 0) : MissionCard;
