using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Risk.Web.Persistence;

/// <summary>
/// The single shared <see cref="JsonSerializerOptions"/> instance for
/// persisting/loading <see cref="GameSnapshot"/> data. Attaches
/// <see cref="ClosedHierarchyResolver"/> as a type-info-resolver modifier
/// (design D3) plus the <see cref="TerritoryIdJsonConverter"/>/
/// <see cref="PlayerIdJsonConverter"/> value converters. Reused by every
/// <see cref="GameSnapshotSerializer"/> call so options (and their internal
/// metadata cache) are built exactly once per process.
/// </summary>
public static class GameJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(ClosedHierarchyResolver.Modify);

        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            WriteIndented = false,
        };

        options.Converters.Add(new TerritoryIdJsonConverter());
        options.Converters.Add(new PlayerIdJsonConverter());

        return options;
    }
}
