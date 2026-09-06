using Risk.Engine.State;
using Risk.Web.Models;

namespace Risk.Web.Persistence;

/// <summary>
/// A serializable snapshot of one saved game: the engine's full
/// <see cref="GameState"/> plus the UI-only <see cref="PlayerConfig"/> values
/// (display name/color/AI flag) that live in <c>Risk.Web</c>, not the engine.
/// <see cref="CurrentSchemaVersion"/> travels alongside every persisted
/// snapshot (see <c>Data.SavedGame.SchemaVersion</c>, PR5) so a saved row
/// written by an older/newer version of this shape can be recognized as
/// incompatible on load instead of crashing (design D5).
/// </summary>
public sealed record GameSnapshot(GameState State, IReadOnlyList<PlayerConfig> Players)
{
    /// <summary>
    /// Bump whenever <see cref="GameState"/>'s or <see cref="PlayerConfig"/>'s
    /// serialized shape changes in a way an older saved row can't be read as.
    /// </summary>
    public const int CurrentSchemaVersion = 1;
}
