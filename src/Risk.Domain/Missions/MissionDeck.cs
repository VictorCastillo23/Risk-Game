using Risk.Domain.Map;

namespace Risk.Domain.Missions;

/// <summary>
/// Builds the standard 14-card secret mission deck: 1 occupy-18, 1
/// occupy-24, 6 eliminate-army (one per <see cref="ArmyId"/> 0..5), and 6
/// conquer-continents cards (4 fixed pairs, 2 with one wildcard slot).
/// Fixed, deterministic order — shuffling/dealing is roadmap 3.2's concern.
/// </summary>
public static class MissionDeck
{
    public static IReadOnlyList<MissionCard> CreateStandard()
    {
        var cards = new List<MissionCard>(14)
        {
            new OccupyTerritories(18, MinArmiesPerTerritory: 2),
            new OccupyTerritories(24, MinArmiesPerTerritory: 1)
        };

        for (var i = 0; i < 6; i++)
        {
            cards.Add(new EliminateArmy(new ArmyId(i)));
        }

        cards.Add(new ConquerContinents([new ContinentId("AS"), new ContinentId("SA")]));
        cards.Add(new ConquerContinents([new ContinentId("AS"), new ContinentId("AF")]));
        cards.Add(new ConquerContinents([new ContinentId("NA"), new ContinentId("AF")]));
        cards.Add(new ConquerContinents([new ContinentId("NA"), new ContinentId("OC")]));

        cards.Add(new ConquerContinents([new ContinentId("EU"), new ContinentId("SA")], WildcardCount: 1));
        cards.Add(new ConquerContinents([new ContinentId("EU"), new ContinentId("OC")], WildcardCount: 1));

        return cards;
    }
}
