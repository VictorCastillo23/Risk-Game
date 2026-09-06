using System.Text.Json;

namespace Risk.Web.Persistence;

/// <summary>
/// Plain-string wrapper around a <see cref="GameSnapshot"/>'s two serialized
/// halves, for stashing into <c>ProtectedSessionStorage</c> across the
/// anonymous-save-then-login redirect (design D2, <c>SavePanel</c>'s
/// anonymous branch / <c>Game.razor</c>'s pending-save rehydration).
///
/// <b>Why this exists instead of stashing a raw <see cref="GameSnapshot"/>
/// directly</b>: confirmed by reflection against the installed 8.0.30
/// shared framework that .NET 8's <c>ProtectedSessionStorage</c> has no
/// constructor overload accepting a custom <see cref="JsonSerializerOptions"/>
/// — it always serializes with the framework's default options, not
/// <see cref="GameJson.Options"/>. A raw <see cref="GameSnapshot"/> cannot
/// round-trip under the default options: <c>TerritoryId</c> is used as a
/// dictionary key throughout <c>GameState</c> (<c>Territories</c>,
/// <c>TerritoriesAssigned.Assignments</c>), which System.Text.Json cannot
/// serialize at all without <see cref="TerritoryIdJsonConverter"/>'s
/// <c>Read/WriteAsPropertyName</c> overrides (it throws
/// <c>NotSupportedException</c> for an unsupported dictionary key type), and
/// <c>GameEvent</c>/<c>Card</c>/<c>MissionCard</c>/<c>GameStatus</c> are
/// polymorphic hierarchies the default options know nothing about. This was
/// a genuine gap in design D2 as originally written (which assumed
/// <c>ProtectedSessionStorage.SetAsync("pending-save", Session.Snapshot())</c>
/// would just work) — caught during PR6 apply, before it could break the
/// anonymous-save flow in production.
///
/// Stashing the two ALREADY-serialized JSON strings (produced by
/// <see cref="GameSnapshotSerializer"/>, which DOES use
/// <see cref="GameJson.Options"/>) sidesteps the problem entirely: a record
/// of two plain <see langword="string"/> properties round-trips fine under
/// any <see cref="JsonSerializerOptions"/>, default or not.
/// </summary>
public sealed record PendingSave(string StateJson, string PlayersJson)
{
    /// <summary>The <c>ProtectedSessionStorage</c> key both <c>SavePanel</c> and <c>Game.razor</c> share.</summary>
    public const string StorageKey = "pending-save";

    public static PendingSave From(GameSnapshot snapshot) =>
        new(
            GameSnapshotSerializer.SerializeState(snapshot.State),
            GameSnapshotSerializer.SerializePlayers(snapshot.Players));

    public GameSnapshot ToSnapshot() =>
        new(
            GameSnapshotSerializer.DeserializeState(StateJson),
            GameSnapshotSerializer.DeserializePlayers(PlayersJson));
}
