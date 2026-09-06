using System.Text.Json;
using Risk.Engine.State;
using Risk.Web.Models;

namespace Risk.Web.Persistence;

/// <summary>
/// Serializes/deserializes a <see cref="GameState"/> and its accompanying
/// <see cref="PlayerConfig"/> list independently, using the shared
/// <see cref="GameJson.Options"/>. <c>Data.SavedGame</c> (PR5) stores them in
/// two separate columns (<c>StateJson</c>/<c>PlayersJson</c>) so the
/// denormalized summary columns can be read without deserializing the full
/// game state.
/// </summary>
public static class GameSnapshotSerializer
{
    public static string SerializeState(GameState state)
        => JsonSerializer.Serialize(state, GameJson.Options);

    public static GameState DeserializeState(string json)
        => JsonSerializer.Deserialize<GameState>(json, GameJson.Options)
            ?? throw new JsonException($"Deserialized {nameof(GameState)} was null.");

    public static string SerializePlayers(IReadOnlyList<PlayerConfig> players)
        => JsonSerializer.Serialize(players, GameJson.Options);

    public static IReadOnlyList<PlayerConfig> DeserializePlayers(string json)
        => JsonSerializer.Deserialize<IReadOnlyList<PlayerConfig>>(json, GameJson.Options)
            ?? throw new JsonException($"Deserialized {nameof(PlayerConfig)} list was null.");
}
