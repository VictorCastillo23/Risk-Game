namespace Risk.Domain.Map;

/// <summary>
/// A continent, its reinforcement bonus, and its member territories.
/// <see cref="Members"/> is populated once <c>WorldMap</c> seeds the full
/// 42-territory board data.
/// </summary>
public sealed record Continent(ContinentId Id, string Name, int Bonus, IReadOnlyList<TerritoryId> Members);
