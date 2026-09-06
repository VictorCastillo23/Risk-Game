using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Web.Models;
using Risk.Web.Persistence;
using Risk.Web.Services;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Persistence;

/// <summary>
/// PR6, task 6.6: <see cref="PendingSave"/> exists specifically because
/// <c>ProtectedSessionStorage</c> cannot serialize a raw <see cref="GameSnapshot"/>
/// under its default (non-<see cref="GameJson.Options"/>) <see cref="System.Text.Json.JsonSerializerOptions"/>
/// — see <see cref="PendingSave"/>'s own doc comment for the reflection-confirmed
/// reasoning. This is the "surrounding logic, one level below the actual
/// storage call" this task's own note points at: <c>ProtectedSessionStorage</c>
/// itself needs a real browser/JS interop to exercise, but the round-trip
/// through <see cref="GameSnapshotSerializer"/> that <see cref="PendingSave"/>
/// wraps is fully testable here, with no browser involved.
/// </summary>
public class PendingSaveTests
{
    [Fact]
    public void From_ThenToSnapshot_RoundTripsAnInProgressGame()
    {
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var rows = new List<PlayerSetupRow>
        {
            new("Ana", "#E53935", false),
            new("Beto", "#1E88E5", false)
        };
        var session = new GameSessionService(engine, new AlwaysAttackerWinsDiceRoller(), new FakeGameStore(), StubAuthenticationStateProvider.Anonymous());
        var startResult = session.Start(rows, GameMode.TwoPlayer);
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(startResult);

        var originalSnapshot = session.Snapshot();

        var pending = PendingSave.From(originalSnapshot);
        var restoredSnapshot = pending.ToSnapshot();

        // Same gotcha as GameSnapshotSerializerTests: GameState's
        // record-generated Equals falls back to reference equality on its
        // dictionary/list members, so canonical-JSON re-serialize equality
        // is the correct proof of a faithful round-trip here, not ==/Equals.
        Assert.Equal(
            GameSnapshotSerializer.SerializeState(originalSnapshot.State),
            GameSnapshotSerializer.SerializeState(restoredSnapshot.State));
        Assert.Equal(
            GameSnapshotSerializer.SerializePlayers(originalSnapshot.Players),
            GameSnapshotSerializer.SerializePlayers(restoredSnapshot.Players));
        Assert.Equal(originalSnapshot.Players.Count, restoredSnapshot.Players.Count);
    }

    [Fact]
    public void From_ProducesPlainStringPayload_SerializableUnderDefaultJsonOptions()
    {
        // The entire point of PendingSave: its own shape (two plain strings)
        // must round-trip under System.Text.Json's DEFAULT options, since
        // that's what ProtectedSessionStorage actually uses. This is the
        // regression guard for the gap this record was created to close.
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var rows = new List<PlayerSetupRow> { new("Ana", "#E53935", false), new("Beto", "#1E88E5", false) };
        var session = new GameSessionService(engine, new AlwaysAttackerWinsDiceRoller(), new FakeGameStore(), StubAuthenticationStateProvider.Anonymous());
        session.Start(rows, GameMode.TwoPlayer);

        var pending = PendingSave.From(session.Snapshot());

        var json = System.Text.Json.JsonSerializer.Serialize(pending);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<PendingSave>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(pending.StateJson, deserialized!.StateJson);
        Assert.Equal(pending.PlayersJson, deserialized.PlayersJson);
    }
}
