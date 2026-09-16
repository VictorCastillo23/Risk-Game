using Risk.Domain.Map;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

/// <summary>
/// PLACEMENT SANITY, NOT ADJACENCY CORRECTNESS. This replaces
/// HexAdjacencyRegressionTests, whose invariant ("two territories sharing a hex
/// edge must be WorldMap.AreAdjacent") has no geometric equivalent once positions
/// are bare points with no shape data. Whether a marker sits inside its painted
/// territory is now verified by manual visual QA only — an accepted reduction
/// (proposal Q8/Risks), recorded here so it is never mistaken for a guarantee.
/// </summary>
public class MarkerPlacementTests
{
    private const double RimGap = 4;

    [Fact]
    public void Coordinates_NoTwoMarkersOverlap()
    {
        var territories = TerritoryLayout.Coordinates.Keys.ToArray();

        for (var i = 0; i < territories.Length; i++)
        {
            for (var j = i + 1; j < territories.Length; j++)
            {
                var a = territories[i];
                var b = territories[j];

                var distance = Distance(TerritoryLayout.Coordinates[a], TerritoryLayout.Coordinates[b]);
                var minDistance = TerritoryLayout.RadiusOf(a) + TerritoryLayout.RadiusOf(b) + RimGap;

                Assert.True(
                    distance >= minDistance,
                    $"{a} and {b} are {distance:F1} apart but need at least {minDistance:F1} (RadiusOf(a) + RadiusOf(b) + {RimGap} rim gap).");
            }
        }
    }

    [Fact]
    public void Coordinates_EveryMarkerFitsFullyInsideTheCanvas()
    {
        Assert.All(TerritoryLayout.Coordinates, entry =>
        {
            var radius = TerritoryLayout.RadiusOf(entry.Key);

            Assert.InRange(entry.Value.X, radius, TerritoryLayout.CanvasWidth - radius);
            Assert.InRange(entry.Value.Y, radius, TerritoryLayout.CanvasHeight - radius);
        });
    }

    [Fact]
    public void RadiusOverrides_AreKnownTerritoriesWithinTheAllowedBand()
    {
        var knownIds = WorldMap.Territories.Select(t => t.Id).ToHashSet();

        Assert.All(knownIds, id =>
        {
            var radius = TerritoryLayout.RadiusOf(id);

            if (radius == TerritoryLayout.MarkerRadius)
            {
                return;
            }

            Assert.InRange(radius, TerritoryLayout.MinRadius, TerritoryLayout.MarkerRadius);
        });
    }

    [Fact]
    public void ContinentLabelAnchors_EachSitsNearItsOwnContinent()
    {
        var centroids = Continents.All.ToDictionary(c => c.Id, c => Centroid(c.Id));
        var spreads = Continents.All.ToDictionary(c => c.Id, c => Spread(c.Id, centroids[c.Id]));

        foreach (var continent in Continents.All)
        {
            var anchor = TerritoryLayout.ContinentLabelAnchors[continent.Id];

            var nearest = Continents.All
                .OrderBy(c => Distance(anchor, centroids[c.Id]))
                .First();

            Assert.Equal(continent.Id, nearest.Id);

            var distanceToOwnCentroid = Distance(anchor, centroids[continent.Id]);
            var allowed = Math.Max(spreads[continent.Id], 120);

            Assert.True(
                distanceToOwnCentroid <= allowed,
                $"{continent.Id} anchor is {distanceToOwnCentroid:F1} from its own centroid, allowed is {allowed:F1}.");
        }
    }

    private static (double X, double Y) Centroid(ContinentId continentId)
    {
        var members = MembersOf(continentId);

        return (members.Average(p => p.X), members.Average(p => p.Y));
    }

    private static double Spread(ContinentId continentId, (double X, double Y) centroid) =>
        MembersOf(continentId).Max(p => Distance(p, centroid));

    private static (double X, double Y)[] MembersOf(ContinentId continentId) =>
        TerritoryLayout.Coordinates
            .Where(kvp => TerritoryLayout.ContinentOf[kvp.Key] == continentId)
            .Select(kvp => kvp.Value)
            .ToArray();

    private static double Distance((double X, double Y) a, (double X, double Y) b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
