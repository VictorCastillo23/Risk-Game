namespace Risk.Web.Models;

/// <summary>
/// Fixed 6-swatch color palette for the setup screen (design D4). UI-level
/// duplicate prevention: a swatch already picked by another row is
/// unavailable, but a row's own current pick stays available to itself.
/// </summary>
public static class PlayerPalette
{
    public static readonly IReadOnlyList<string> Swatches =
    [
        "#D62828", // red
        "#1D4ED8", // blue
        "#2E7D32", // green
        "#F2C14E", // yellow
        "#7B2CBF", // purple
        "#212529" // black
    ];

    /// <summary>
    /// True unless another row (any index other than <paramref name="rowIndex"/>)
    /// has already selected <paramref name="swatch"/>.
    /// </summary>
    public static bool IsAvailable(string swatch, int rowIndex, IReadOnlyList<string?> selectedColors)
    {
        for (var i = 0; i < selectedColors.Count; i++)
        {
            if (i != rowIndex && selectedColors[i] == swatch)
            {
                return false;
            }
        }

        return true;
    }
}
