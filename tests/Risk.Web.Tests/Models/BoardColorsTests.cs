using Risk.Domain.Players;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class BoardColorsTests
{
    [Fact]
    public void OwnerColor_WithConfiguredOwner_ReturnsThatPlayersColorHex()
    {
        var owner = new PlayerId(1);
        var players = new Dictionary<PlayerId, PlayerConfig>
        {
            [new PlayerId(0)] = new PlayerConfig(new PlayerId(0), "Ana", "#FF0000", false),
            [owner] = new PlayerConfig(owner, "Beto", "#00FF00", false)
        };

        var color = BoardColors.OwnerColor(owner, players);

        Assert.Equal("#00FF00", color);
    }

    [Fact]
    public void OwnerColor_WithUnknownOwner_ReturnsFallbackColor()
    {
        var players = new Dictionary<PlayerId, PlayerConfig>
        {
            [new PlayerId(0)] = new PlayerConfig(new PlayerId(0), "Ana", "#FF0000", false)
        };

        var color = BoardColors.OwnerColor(new PlayerId(9), players);

        Assert.Equal(BoardColors.UnknownOwnerColor, color);
    }

    [Fact]
    public void UnclaimedColor_DiffersFromUnknownOwnerColor()
    {
        Assert.NotEqual(BoardColors.UnknownOwnerColor, BoardColors.UnclaimedColor);
    }

    [Fact]
    public void UnclaimedColor_DiffersFromEveryConfiguredPlayerColor()
    {
        var players = new Dictionary<PlayerId, PlayerConfig>
        {
            [new PlayerId(0)] = new PlayerConfig(new PlayerId(0), "Ana", "#FF0000", false),
            [new PlayerId(1)] = new PlayerConfig(new PlayerId(1), "Beto", "#00FF00", false)
        };

        Assert.DoesNotContain(BoardColors.UnclaimedColor, players.Values.Select(p => p.ColorHex));
    }
}
