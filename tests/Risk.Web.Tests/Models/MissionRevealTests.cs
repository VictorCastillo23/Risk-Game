using Risk.Domain.Players;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class MissionRevealTests
{
    private static readonly PlayerId PlayerA = new(0);
    private static readonly PlayerId PlayerB = new(1);

    [Fact]
    public void Hidden_IsRevealedForNobody()
    {
        Assert.False(MissionReveal.Hidden.IsRevealedFor(PlayerA));
        Assert.False(MissionReveal.Hidden.IsRevealedFor(PlayerB));
    }

    [Fact]
    public void Toggle_FromHidden_RevealsForThatPlayer()
    {
        var reveal = MissionReveal.Hidden.Toggle(PlayerA);

        Assert.True(reveal.IsRevealedFor(PlayerA));
    }

    [Fact]
    public void Toggle_Twice_HidesAgain()
    {
        var reveal = MissionReveal.Hidden.Toggle(PlayerA).Toggle(PlayerA);

        Assert.False(reveal.IsRevealedFor(PlayerA));
    }

    [Fact]
    public void Toggle_RevealedForOnePlayer_IsNotRevealedForAnother()
    {
        // The reset-on-player-change property (design 3.4-D3), as a value
        // invariant rather than a lifecycle hook: revealing for A never
        // reveals for B, so a fresh render for B is hidden without any
        // explicit reset step being required.
        var reveal = MissionReveal.Hidden.Toggle(PlayerA);

        Assert.False(reveal.IsRevealedFor(PlayerB));
    }
}
