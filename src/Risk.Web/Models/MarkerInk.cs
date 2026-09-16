using System.Globalization;

namespace Risk.Web.Models;

/// <summary>
/// Ring colors for a territory marker's two contrast rings (design D8).
/// A marker is three concentric circles: the owner-colored fill, an inner
/// contrast ring chosen by the fill's relative luminance, then this
/// constant outer ink ring. Because the fill is never directly adjacent to
/// the map artwork (the ink ring always is), each pairing here is a known
/// constant checkable in a unit test rather than a per-territory visual
/// spot-check against 42 different backgrounds.
/// </summary>
public static class MarkerInk
{
    /// <summary>
    /// Constant outer ring color, the one ring that ever touches the map
    /// artwork. Matches <c>--ink</c> in <c>wwwroot/app.css</c>.
    /// </summary>
    public const string Outer = "#2b2118";

    /// <summary>
    /// Inner ring color used when the fill is dark (relative luminance below
    /// <see cref="LuminanceThreshold"/>) — a light ring reaches 3:1 against a
    /// dark fill. Matches <c>--parchment-light</c> in <c>wwwroot/app.css</c>.
    /// </summary>
    public const string InnerOnDark = "#f5ead0";

    /// <summary>
    /// Inner ring color used when the fill is light (relative luminance at
    /// or above <see cref="LuminanceThreshold"/>) — a dark ring reaches 3:1
    /// against a light fill. Same value as <see cref="Outer"/>.
    /// </summary>
    public const string InnerOnLight = "#2b2118";

    /// <summary>
    /// WCAG relative-luminance threshold below which a fill is treated as
    /// "dark" for inner-ring selection. At exactly this threshold the worst
    /// case still clears 3:1 on both sides (design D8), so every sRGB fill
    /// — not only today's palette — is covered.
    /// </summary>
    private const double LuminanceThreshold = 0.18;

    /// <summary>The inner contrast ring color for <paramref name="fillHex"/>.</summary>
    public static string InnerRingFor(string fillHex) =>
        RelativeLuminance(fillHex) < LuminanceThreshold ? InnerOnDark : InnerOnLight;

    /// <summary>WCAG 2.x relative luminance of a <c>#RRGGBB</c> hex color, in [0, 1].</summary>
    public static double RelativeLuminance(string hexColor)
    {
        var (r, g, b) = ParseHex(hexColor);

        var rLin = ToLinear(r);
        var gLin = ToLinear(g);
        var bLin = ToLinear(b);

        return (0.2126 * rLin) + (0.7152 * gLin) + (0.0722 * bLin);
    }

    /// <summary>WCAG contrast ratio between two <c>#RRGGBB</c> hex colors, in [1, 21].</summary>
    public static double ContrastRatio(string hex1, string hex2)
    {
        var l1 = RelativeLuminance(hex1);
        var l2 = RelativeLuminance(hex2);

        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static (int R, int G, int B) ParseHex(string hexColor)
    {
        var span = hexColor.AsSpan().TrimStart('#');

        var r = int.Parse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = int.Parse(span.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = int.Parse(span.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return (r, g, b);
    }

    private static double ToLinear(int channel)
    {
        var normalized = channel / 255.0;

        return normalized <= 0.03928
            ? normalized / 12.92
            : Math.Pow((normalized + 0.055) / 1.055, 2.4);
    }
}
