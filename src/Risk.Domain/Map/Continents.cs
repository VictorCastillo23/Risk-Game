namespace Risk.Domain.Map;

/// <summary>
/// The fixed set of 6 classic Risk continents and their reinforcement
/// bonuses, per the proposal's pinned decision. <see cref="Continent.Members"/>
/// is left empty here; <c>WorldMap</c> assigns real membership once the full
/// 42-territory board is seeded.
/// </summary>
public static class Continents
{
    public static IReadOnlyList<Continent> All { get; } =
    [
        new Continent(new ContinentId("NA"), "North America", 5, []),
        new Continent(new ContinentId("SA"), "South America", 2, []),
        new Continent(new ContinentId("EU"), "Europe", 5, []),
        new Continent(new ContinentId("AF"), "Africa", 3, []),
        new Continent(new ContinentId("AS"), "Asia", 7, []),
        new Continent(new ContinentId("OC"), "Oceania", 2, [])
    ];
}
