using System.Text.Json;
using System.Text.Json.Serialization;
using Risk.Domain.Players;

namespace Risk.Web.Persistence;

/// <summary>
/// Serializes <see cref="PlayerId"/> as its raw integer value instead of
/// System.Text.Json's default <c>{ "Value": 0 }</c> wrapper. Overrides
/// <see cref="ReadAsPropertyName"/>/<see cref="WriteAsPropertyName"/> (not
/// just <see cref="Read"/>/<see cref="Write"/>) because
/// <see cref="Risk.Engine.Events.HeadquartersRevealed.Headquarters"/> uses
/// <see cref="PlayerId"/> as a dictionary key.
/// </summary>
public sealed class PlayerIdJsonConverter : JsonConverter<PlayerId>
{
    public override PlayerId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, PlayerId value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Value);

    public override PlayerId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(int.Parse(reader.GetString() ?? throw new JsonException($"{nameof(PlayerId)} property name cannot be null.")));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, PlayerId value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
