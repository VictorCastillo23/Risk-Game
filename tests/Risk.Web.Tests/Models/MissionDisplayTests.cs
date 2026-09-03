using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class MissionDisplayTests
{
    private static readonly IReadOnlyDictionary<PlayerId, PlayerConfig> Players = new Dictionary<PlayerId, PlayerConfig>
    {
        [new PlayerId(2)] = new PlayerConfig(new PlayerId(2), "Ana", "#FF0000", false)
    };

    [Fact]
    public void Label_OccupyTerritories_DefaultMinArmies_RendersWithoutArmyClause()
    {
        var mission = new OccupyTerritories(24, 1);

        Assert.Equal("Ocupá 24 territorios.", MissionDisplay.Label(mission, Players));
    }

    [Fact]
    public void Label_OccupyTerritories_MinArmiesAtLeastTwo_RendersArmyClause()
    {
        var mission = new OccupyTerritories(18, 2);

        Assert.Equal("Ocupá 18 territorios con al menos 2 tropas en cada uno.", MissionDisplay.Label(mission, Players));
    }

    [Fact]
    public void Label_EliminateArmy_SeatedTarget_RendersPlayerName()
    {
        var mission = new EliminateArmy(new ArmyId(2));

        Assert.Equal("Destruí todas las tropas de Ana.", MissionDisplay.Label(mission, Players));
    }

    [Fact]
    public void Label_EliminateArmy_UnknownArmyId_FallsBackWithoutThrowing()
    {
        var mission = new EliminateArmy(new ArmyId(4));

        Assert.Equal("Destruí todas las tropas de Ejército 5.", MissionDisplay.Label(mission, Players));
    }

    [Fact]
    public void Label_ConquerContinents_NoWildcard_JoinsRequiredContinentsWithY()
    {
        var mission = new ConquerContinents([new ContinentId("NA"), new ContinentId("AF")], 0);

        Assert.Equal("Conquistá América del Norte y África.", MissionDisplay.Label(mission, Players));
    }

    [Fact]
    public void Label_ConquerContinents_OneWildcard_UsesSingularContinentWord()
    {
        var mission = new ConquerContinents([new ContinentId("AS"), new ContinentId("SA")], 1);

        Assert.Equal(
            "Conquistá Asia y América del Sur, más 1 continente más de tu elección.",
            MissionDisplay.Label(mission, Players));
    }

    [Fact]
    public void Label_ConquerContinents_TwoWildcards_UsesPluralContinentesWord()
    {
        var mission = new ConquerContinents([new ContinentId("AS"), new ContinentId("SA")], 2);

        Assert.Equal(
            "Conquistá Asia y América del Sur, más 2 continentes más de tu elección.",
            MissionDisplay.Label(mission, Players));
    }

    [Theory]
    [InlineData("Ocupa ")]
    [InlineData("Destruye ")]
    [InlineData("Conquista ")]
    public void Label_NeverProducesTuteoForms(string tuteoForm)
    {
        // Design 3.4-D6: mission text is voseo, matching CardPanel's "Tenés…
        // debés…". reglasrisk.md prints these missions in tuteo, but it is
        // not the source of truth for UI copy — this guards against a future
        // contributor "restoring" that wording.
        var missions = new MissionCard[]
        {
            new OccupyTerritories(24, 1),
            new OccupyTerritories(18, 2),
            new EliminateArmy(new ArmyId(2)),
            new EliminateArmy(new ArmyId(4)),
            new ConquerContinents([new ContinentId("NA"), new ContinentId("AF")], 0),
            new ConquerContinents([new ContinentId("AS"), new ContinentId("SA")], 2)
        };

        foreach (var mission in missions)
        {
            Assert.DoesNotContain(tuteoForm, MissionDisplay.Label(mission, Players));
        }
    }
}
