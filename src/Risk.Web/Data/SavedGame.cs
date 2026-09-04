namespace Risk.Web.Data;

/// <summary>
/// The single saved-game row for an owning account. <see cref="OwnerId"/> is
/// both the primary key AND the foreign key to <c>AspNetUsers.Id</c> (cascade
/// delete) — a shared-primary-key one-to-one relationship, so "one saved
/// game per account" (spec's "One saved game per account, overwritten on
/// re-save") is structurally enforced by the schema itself, no unique index
/// needed (design's Schema section).
/// </summary>
public sealed class SavedGame
{
    /// <summary>Primary key AND foreign key to <c>AspNetUsers.Id</c>.</summary>
    public required string OwnerId { get; set; }

    /// <summary>Serialized <c>GameState</c> (<see cref="Persistence.GameSnapshotSerializer.SerializeState"/>).</summary>
    public required string StateJson { get; set; }

    /// <summary>Serialized <c>PlayerConfig[]</c> (<see cref="Persistence.GameSnapshotSerializer.SerializePlayers"/>).</summary>
    public required string PlayersJson { get; set; }

    /// <summary>Denormalized <c>GameMode</c> (string, not int — survives enum reordering), for the resume list.</summary>
    public required string Mode { get; set; }

    /// <summary>Denormalized player count, for the resume list.</summary>
    public required int PlayerCount { get; set; }

    /// <summary>Denormalized <c>TurnPhase</c> (string), for the resume list.</summary>
    public required string Phase { get; set; }

    public required DateTime SavedAtUtc { get; set; }

    /// <summary>
    /// Matches <see cref="Persistence.GameSnapshot.CurrentSchemaVersion"/> at
    /// the time this row was written (design D5) — lets a load recognize an
    /// incompatible older/newer shape instead of crashing on deserialize.
    /// </summary>
    public required int SchemaVersion { get; set; }
}
