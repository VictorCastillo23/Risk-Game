using Risk.Domain.Map;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

/// <summary>
/// Guards the class of bug where two territories' hexagons visually share an
/// edge on <see cref="TerritoryLayout"/>'s low-poly board even though
/// <see cref="WorldMap"/>'s rule graph does not consider them adjacent (or
/// vice versa breaks a real border). Layout is presentation-only and must
/// never mislead a player about which territories actually border each other.
/// </summary>
public class HexAdjacencyRegressionTests
{
    private const double Epsilon = 0.01;

    /// <summary>Two territories share a hex edge when their polygons have at least 2 (near-)coincident vertices.</summary>
    private static bool SharesHexEdge(TerritoryId a, TerritoryId b)
    {
        var polygonA = TerritoryLayout.Polygons[a];
        var polygonB = TerritoryLayout.Polygons[b];

        var sharedVertexCount = polygonA.Count(pointA =>
            polygonB.Any(pointB =>
                Math.Abs(pointA.X - pointB.X) < Epsilon &&
                Math.Abs(pointA.Y - pointB.Y) < Epsilon));

        return sharedVertexCount >= 2;
    }

    /// <summary>
    /// Pre-existing false hex-adjacencies discovered incidentally by this
    /// regression check. Tracked here instead of silently ignored so the
    /// class of bug stays caught for every other pair while a specific one
    /// awaits its own dedicated fix. Empty now that both the Asia pairs and
    /// the Africa pair (Egypt-Congo) have been fixed.
    /// </summary>
    private static readonly (string A, string B)[] KnownPreExistingFalseAdjacencies = [];

    private static bool IsKnownPreExisting(TerritoryId a, TerritoryId b) =>
        KnownPreExistingFalseAdjacencies.Any(pair =>
            (pair.A == a.Value && pair.B == b.Value) ||
            (pair.A == b.Value && pair.B == a.Value));

    [Fact]
    public void Polygons_NoTwoTerritoriesShareAHexEdge_UnlessWorldMapConsidersThemAdjacent()
    {
        var territories = WorldMap.Territories.Select(t => t.Id).ToArray();

        for (var i = 0; i < territories.Length; i++)
        {
            for (var j = i + 1; j < territories.Length; j++)
            {
                var a = territories[i];
                var b = territories[j];

                if (IsKnownPreExisting(a, b))
                {
                    continue;
                }

                if (SharesHexEdge(a, b))
                {
                    Assert.True(
                        WorldMap.AreAdjacent(a, b),
                        $"{a} and {b} visually share a hex edge on the board but are not adjacent in WorldMap.");
                }
            }
        }
    }

    public static IEnumerable<object[]> PreviouslyFalseAsiaPairs()
    {
        yield return [new TerritoryId("Yakutsk"), new TerritoryId("Mongolia")];
        yield return [new TerritoryId("Irkutsk"), new TerritoryId("China")];
        yield return [new TerritoryId("Irkutsk"), new TerritoryId("Afghanistan")];
        yield return [new TerritoryId("Mongolia"), new TerritoryId("Siam")];
        yield return [new TerritoryId("Japan"), new TerritoryId("Siam")];
        yield return [new TerritoryId("China"), new TerritoryId("MiddleEast")];
    }

    [Theory]
    [MemberData(nameof(PreviouslyFalseAsiaPairs))]
    public void Polygons_Asia_PreviouslyFalseAdjacentPairs_NoLongerShareAHexEdge(TerritoryId a, TerritoryId b)
    {
        Assert.False(WorldMap.AreAdjacent(a, b), $"{a}-{b} must not be a real WorldMap adjacency (sanity check).");
        Assert.False(SharesHexEdge(a, b), $"{a} and {b} still falsely share a hex edge.");
    }

    public static IEnumerable<object[]> PreviouslyCorrectAsiaPairs()
    {
        yield return [new TerritoryId("Ural"), new TerritoryId("Siberia")];
        yield return [new TerritoryId("Siberia"), new TerritoryId("Yakutsk")];
        yield return [new TerritoryId("Siberia"), new TerritoryId("Irkutsk")];
        yield return [new TerritoryId("Yakutsk"), new TerritoryId("Irkutsk")];
        yield return [new TerritoryId("Yakutsk"), new TerritoryId("Kamchatka")];
        yield return [new TerritoryId("Kamchatka"), new TerritoryId("Mongolia")];
        yield return [new TerritoryId("Kamchatka"), new TerritoryId("Japan")];
        yield return [new TerritoryId("Irkutsk"), new TerritoryId("Mongolia")];
        yield return [new TerritoryId("Mongolia"), new TerritoryId("Japan")];
        yield return [new TerritoryId("Mongolia"), new TerritoryId("China")];
        yield return [new TerritoryId("China"), new TerritoryId("Afghanistan")];
        yield return [new TerritoryId("China"), new TerritoryId("India")];
        yield return [new TerritoryId("China"), new TerritoryId("Siam")];
        yield return [new TerritoryId("Afghanistan"), new TerritoryId("MiddleEast")];
        yield return [new TerritoryId("MiddleEast"), new TerritoryId("India")];
        yield return [new TerritoryId("India"), new TerritoryId("Siam")];
    }

    [Theory]
    [MemberData(nameof(PreviouslyCorrectAsiaPairs))]
    public void Polygons_Asia_PreviouslyCorrectAdjacentPairs_StillShareAHexEdge(TerritoryId a, TerritoryId b)
    {
        Assert.True(WorldMap.AreAdjacent(a, b), $"{a}-{b} must be a real WorldMap adjacency (sanity check).");
        Assert.True(SharesHexEdge(a, b), $"{a} and {b} no longer share a hex edge — a real border was broken.");
    }
}
