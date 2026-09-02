using Risk.Engine.State;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class PhaseDisplayTests
{
    [Theory]
    [InlineData(TurnPhase.Claim, "Reclamo de territorios")]
    [InlineData(TurnPhase.Setup, "Configuración")]
    [InlineData(TurnPhase.SelectHeadquarters, "Selección de cuartel general")]
    [InlineData(TurnPhase.Reinforce, "Refuerzo")]
    [InlineData(TurnPhase.Attack, "Ataque")]
    [InlineData(TurnPhase.Fortify, "Fortificación")]
    public void Label_CoversEveryTurnPhase(TurnPhase phase, string expected)
    {
        Assert.Equal(expected, PhaseDisplay.Label(phase));
    }

    [Fact]
    public void Label_NeverFallsBackToRawEnumToString()
    {
        foreach (var phase in Enum.GetValues<TurnPhase>())
        {
            Assert.NotEqual(phase.ToString(), PhaseDisplay.Label(phase));
        }
    }
}
