using System.Text.Json;

namespace Risk.Web.Persistence;

/// <summary>
/// The single shared <see cref="JsonSerializerOptions"/> instance for
/// persisting/loading <see cref="GameSnapshot"/> data. Reused by every
/// <see cref="GameSnapshotSerializer"/> call so options (and their internal
/// metadata cache) are built exactly once per process.
/// </summary>
public static class GameJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
        => new()
        {
            WriteIndented = false,
        };
}
