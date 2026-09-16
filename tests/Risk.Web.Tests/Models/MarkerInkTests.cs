using System.Text.RegularExpressions;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class MarkerInkTests
{
    public static IEnumerable<object[]> AllFills()
    {
        foreach (var swatch in PlayerPalette.Swatches)
        {
            yield return new object[] { swatch };
        }

        yield return new object[] { BoardColors.UnclaimedColor };
        yield return new object[] { BoardColors.UnknownOwnerColor };
        yield return new object[] { BoardColors.NeutralColor };
    }

    [Theory]
    [MemberData(nameof(AllFills))]
    public void InnerRingFor_ReachesThreeToOneAgainstEveryFill(string fill)
    {
        var ratio = MarkerInk.ContrastRatio(fill, MarkerInk.InnerRingFor(fill));

        Assert.True(ratio >= 3.0, $"{fill} inner ring contrast was {ratio:F2}:1, expected >= 3.0:1.");
    }

    // Average local-artwork RGB sampled at the outer ring's true radius for
    // the 5 territories flagged by sdd-verify's independent pixel-sampling
    // script (Kamchatka, Japan, Indonesia, NewGuinea, Madagascar all sit on
    // dark island/coastal-shading art). Against the default MarkerInk.Outer
    // ("#2b2118") these 5 fail WCAG 1.4.11's 3:1 floor; MarkerInk.OuterOnDarkArt
    // is the fix, applied to exactly these 5 via TerritoryLayout.NeedsLightOuterRing.
    public static IEnumerable<object[]> DarkArtSamples()
    {
        yield return new object[] { "Kamchatka", "#343221" };
        yield return new object[] { "Japan", "#686d25" };
        yield return new object[] { "Indonesia", "#853e3d" };
        yield return new object[] { "NewGuinea", "#934d47" };
        yield return new object[] { "Madagascar", "#b2471c" };
    }

    [Theory]
    [MemberData(nameof(DarkArtSamples))]
    public void OuterOnDarkArt_ReachesThreeToOneAgainstMeasuredDarkArt(string territory, string measuredArtHex)
    {
        var ratio = MarkerInk.ContrastRatio(measuredArtHex, MarkerInk.OuterOnDarkArt);

        Assert.True(ratio >= 3.0, $"{territory} outer ring contrast against measured art was {ratio:F2}:1, expected >= 3.0:1.");
    }

    [Fact]
    public void RingColors_StillMatchTheCssCustomProperties()
    {
        var cssPath = FindAppCss();
        var css = File.ReadAllText(cssPath);

        var ink = ExtractCustomProperty(css, "--ink");
        var parchmentLight = ExtractCustomProperty(css, "--parchment-light");

        Assert.Equal(MarkerInk.Outer, ink, ignoreCase: true);
        Assert.Equal(MarkerInk.InnerOnLight, ink, ignoreCase: true);
        Assert.Equal(MarkerInk.InnerOnDark, parchmentLight, ignoreCase: true);
    }

    private static string FindAppCss()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Risk.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Could not locate Risk.sln from AppContext.BaseDirectory.");
        }

        return Path.Combine(dir.FullName, "src", "Risk.Web", "wwwroot", "app.css");
    }

    private static string ExtractCustomProperty(string css, string name)
    {
        var match = Regex.Match(css, $@"{Regex.Escape(name)}:\s*(#[0-9a-fA-F]{{6}})");

        if (!match.Success)
        {
            throw new InvalidOperationException($"Custom property {name} not found in app.css.");
        }

        return match.Groups[1].Value;
    }
}
