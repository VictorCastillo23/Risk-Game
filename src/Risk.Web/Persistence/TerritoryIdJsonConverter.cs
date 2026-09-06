using System.Text.Json;
using System.Text.Json.Serialization;
using Risk.Domain.Map;

namespace Risk.Web.Persistence;

/// <summary>
/// Serializes <see cref="TerritoryId"/> as its raw string value instead of
/// System.Text.Json's default <c>{ "Value": "..." }</c> wrapper. Overrides
/// <see cref="ReadAsPropertyName"/>/<see cref="WriteAsPropertyName"/> (not
/// just <see cref="Read"/>/<see cref="Write"/>) because
/// <see cref="Risk.Engine.State.GameState.Territories"/> and
/// <see cref="Risk.Engine.Events.TerritoriesAssigned.Assignments"/> both use
/// <see cref="TerritoryId"/> as a dictionary key.
/// </summary>
public sealed class TerritoryIdJsonConverter : JsonConverter<TerritoryId>
{
    public override TerritoryId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? throw new JsonException($"{nameof(TerritoryId)} value cannot be null."));

    public override void Write(Utf8JsonWriter writer, TerritoryId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);

    public override TerritoryId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? throw new JsonException($"{nameof(TerritoryId)} property name cannot be null."));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, TerritoryId value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value);
}
