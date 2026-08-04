namespace Risk.Domain.Map;

/// <summary>
/// The fixed set of 6 classic Risk continents, their reinforcement bonuses
/// (per the proposal's pinned decision), and their real member territories
/// (derived from <see cref="WorldMap.Territories"/>).
/// </summary>
public static class Continents
{
    private static readonly (string Id, string Name, int Bonus)[] Metadata =
    [
        ("NA", "North America", 5),
        ("SA", "South America", 2),
        ("EU", "Europe", 5),
        ("AF", "Africa", 3),
        ("AS", "Asia", 7),
        ("OC", "Oceania", 2)
    ];

    public static IReadOnlyList<Continent> All { get; } = BuildAll();

    private static IReadOnlyList<Continent> BuildAll()
    {
        var continents = new List<Continent>(Metadata.Length);

        foreach (var (id, name, bonus) in Metadata)
        {
            var continentId = new ContinentId(id);
            var members = WorldMap.Territories
                .Where(t => t.ContinentId == continentId)
                .Select(t => t.Id)
                .ToArray();

            continents.Add(new Continent(continentId, name, bonus, members));
        }

        return continents;
    }
}
