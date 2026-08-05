using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class PlayerPaletteTests
{
    [Fact]
    public void Swatches_HasSixDistinctColors()
    {
        Assert.Equal(6, PlayerPalette.Swatches.Count);
        Assert.Equal(6, PlayerPalette.Swatches.Distinct().Count());
    }

    [Fact]
    public void IsAvailable_WhenNoRowHasSelectedIt_ReturnsTrue()
    {
        string?[] selected = [null, null, null];

        Assert.True(PlayerPalette.IsAvailable(PlayerPalette.Swatches[0], rowIndex: 0, selected));
    }

    [Fact]
    public void IsAvailable_WhenAnotherRowHasSelectedIt_ReturnsFalse()
    {
        var color = PlayerPalette.Swatches[0];
        string?[] selected = [null, color, null];

        Assert.False(PlayerPalette.IsAvailable(color, rowIndex: 0, selected));
    }

    [Fact]
    public void IsAvailable_WhenTheSameRowHasSelectedIt_StillReturnsTrue()
    {
        var color = PlayerPalette.Swatches[0];
        string?[] selected = [color, null, null];

        Assert.True(PlayerPalette.IsAvailable(color, rowIndex: 0, selected));
    }

    [Fact]
    public void IsAvailable_ForAColorNoOneSelected_ReturnsTrueRegardlessOfOtherSelections()
    {
        string?[] selected = [PlayerPalette.Swatches[0], PlayerPalette.Swatches[1], null];

        Assert.True(PlayerPalette.IsAvailable(PlayerPalette.Swatches[2], rowIndex: 2, selected));
    }
}
