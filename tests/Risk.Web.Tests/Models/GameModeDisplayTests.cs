using Risk.Engine.State;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class GameModeDisplayTests
{
    [Theory]
    [InlineData(GameMode.Classic, "Clásico")]
    [InlineData(GameMode.SecretMission, "Misión secreta")]
    [InlineData(GameMode.TwoPlayer, "Dos jugadores")]
    [InlineData(GameMode.Capital, "Capital")]
    public void Label_CoversEveryGameMode(GameMode mode, string expected)
    {
        Assert.Equal(expected, GameModeDisplay.Label(mode));
    }

    [Fact]
    public void Label_CoversEveryGameModeValueExhaustively()
    {
        // Unlike PhaseDisplay/Claim, GameMode.Capital's Spanish label is
        // legitimately the same word as its enum name ("Capital"), so this
        // asserts exhaustive coverage (no missing arm throws) rather than
        // "differs from ToString()".
        foreach (var mode in Enum.GetValues<GameMode>())
        {
            Assert.False(string.IsNullOrWhiteSpace(GameModeDisplay.Label(mode)));
        }
    }
}
