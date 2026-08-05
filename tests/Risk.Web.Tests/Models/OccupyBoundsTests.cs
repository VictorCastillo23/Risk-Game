using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class OccupyBoundsTests
{
    [Fact]
    public void Clamp_ValueWithinRange_ReturnsValueUnchanged()
    {
        Assert.Equal(3, OccupyBounds.Clamp(3, min: 1, max: 5));
    }

    [Fact]
    public void Clamp_ValueBelowMinimum_ReturnsMinimum()
    {
        Assert.Equal(2, OccupyBounds.Clamp(0, min: 2, max: 5));
    }

    [Fact]
    public void Clamp_ValueAboveMaximum_ReturnsMaximum()
    {
        Assert.Equal(5, OccupyBounds.Clamp(9, min: 2, max: 5));
    }

    [Fact]
    public void Clamp_WhenMinimumEqualsMaximum_AlwaysReturnsThatFixedValue()
    {
        Assert.Equal(4, OccupyBounds.Clamp(1, min: 4, max: 4));
        Assert.Equal(4, OccupyBounds.Clamp(9, min: 4, max: 4));
    }

    [Fact]
    public void IsFixed_WhenMinimumEqualsMaximum_ReturnsTrue()
    {
        Assert.True(OccupyBounds.IsFixed(min: 3, max: 3));
    }

    [Fact]
    public void IsFixed_WhenRangeHasMoreThanOneChoice_ReturnsFalse()
    {
        Assert.False(OccupyBounds.IsFixed(min: 1, max: 3));
    }
}
