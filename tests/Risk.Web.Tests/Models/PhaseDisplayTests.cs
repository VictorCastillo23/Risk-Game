using Risk.Engine.State;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class PhaseDisplayTests
{
    [Theory]
    [InlineData(TurnPhase.Setup, "Configuración")]
    [InlineData(TurnPhase.Reinforce, "Refuerzo")]
    [InlineData(TurnPhase.Attack, "Ataque")]
    [InlineData(TurnPhase.Fortify, "Fortificación")]
    public void Label_CoversEveryTurnPhase(TurnPhase phase, string expected)
    {
        Assert.Equal(expected, PhaseDisplay.Label(phase));
    }
}
