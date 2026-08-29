namespace Risk.Web.Models;

/// <summary>
/// Pure pointy-top axial-hex math shared by <see cref="TerritoryLayout"/> to
/// generate low-poly continent silhouettes without hand-authoring per-territory
/// polygon vertices — only a tiny (Q, R) axial coordinate per territory is
/// hand-placed there; the actual pixel geometry is derived here.
/// </summary>
public static class HexGrid
{
    private const double Sqrt3 = 1.7320508075688772;

    /// <summary>Pixel center of the hex at axial (<paramref name="q"/>, <paramref name="r"/>), scaled by <paramref name="size"/> and offset by a local grid's origin.</summary>
    public static (double X, double Y) AxialToPixel(int q, int r, double size, double originX, double originY) =>
        (originX + size * (Sqrt3 * q + Sqrt3 / 2 * r), originY + size * (1.5 * r));

    /// <summary>The 6 vertices of a pointy-top regular hexagon centered at (<paramref name="centerX"/>, <paramref name="centerY"/>) with circumradius <paramref name="size"/>.</summary>
    public static IReadOnlyList<(double X, double Y)> Corners(double centerX, double centerY, double size)
    {
        var corners = new (double X, double Y)[6];

        for (var i = 0; i < 6; i++)
        {
            var angle = Math.PI / 180 * (60 * i - 30);
            corners[i] = (centerX + size * Math.Cos(angle), centerY + size * Math.Sin(angle));
        }

        return corners;
    }
}
