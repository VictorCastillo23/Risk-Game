using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class HexGridTests
{
    [Fact]
    public void Corners_ReturnsExactlySixPoints()
    {
        var corners = HexGrid.Corners(0, 0, 40);

        Assert.Equal(6, corners.Count);
    }

    [Fact]
    public void Corners_AreAllEquidistantFromTheCenter_ARegularHexagon()
    {
        const double centerX = 100;
        const double centerY = 50;
        const double size = 40;

        var corners = HexGrid.Corners(centerX, centerY, size);

        Assert.All(corners, corner =>
        {
            var distance = Math.Sqrt(Math.Pow(corner.X - centerX, 2) + Math.Pow(corner.Y - centerY, 2));
            Assert.True(Math.Abs(distance - size) < 0.0001);
        });
    }

    [Fact]
    public void Corners_HasNoNaNOrInfiniteValues()
    {
        var corners = HexGrid.Corners(10, 20, 40);

        Assert.All(corners, corner =>
        {
            Assert.False(double.IsNaN(corner.X) || double.IsInfinity(corner.X));
            Assert.False(double.IsNaN(corner.Y) || double.IsInfinity(corner.Y));
        });
    }

    [Fact]
    public void AxialToPixel_AtOriginCell_ReturnsTheOriginItself()
    {
        var (x, y) = HexGrid.AxialToPixel(0, 0, 40, originX: 100, originY: 200);

        Assert.Equal(100, x, precision: 6);
        Assert.Equal(200, y, precision: 6);
    }

    [Fact]
    public void AxialToPixel_DifferentCells_ProduceDistinctCenters()
    {
        var a = HexGrid.AxialToPixel(0, 0, 40, 0, 0);
        var b = HexGrid.AxialToPixel(1, 0, 40, 0, 0);
        var c = HexGrid.AxialToPixel(0, 1, 40, 0, 0);

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(b, c);
    }

    [Fact]
    public void AxialToPixel_HasNoNaNOrInfiniteValues()
    {
        var (x, y) = HexGrid.AxialToPixel(3, -1, 40, 90, 160);

        Assert.False(double.IsNaN(x) || double.IsInfinity(x));
        Assert.False(double.IsNaN(y) || double.IsInfinity(y));
    }
}
