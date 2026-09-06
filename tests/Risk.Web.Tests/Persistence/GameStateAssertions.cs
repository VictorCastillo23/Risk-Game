using Risk.Domain.Missions;
using Risk.Engine.State;
using Risk.Web.Persistence;
using Xunit;

namespace Risk.Web.Tests.Persistence;

/// <summary>
/// Reusable assertion helpers for <see cref="GameState"/> round-trip tests
/// (task 4.4). Deliberately avoids <c>deserialized == original</c> or
/// <c>deserialized.Equals(original)</c> everywhere — <see cref="GameState"/>'s
/// record-generated <c>Equals</c> falls back to reference equality on its
/// <c>IReadOnlyDictionary</c>/<c>IReadOnlyList</c> members (design's
/// documented gotcha, see <c>GameSnapshotSerializerTests</c>), so that
/// comparison would be false even on a perfect round-trip. Kept alongside
/// the serializer tests so PR5's future save/resume integration test can
/// reuse this instead of duplicating assertion logic.
/// </summary>
internal static class GameStateAssertions
{
    /// <summary>
    /// Serializes <paramref name="state"/>, deserializes it back, and
    /// re-serializes the result, asserting the two JSON strings are
    /// byte-for-byte identical (the canonical-JSON equality check). Returns
    /// the deserialized <see cref="GameState"/> so callers can layer
    /// additional structural assertions (<see cref="AssertStructurallyEqual"/>
    /// or bespoke per-test checks) on top.
    /// </summary>
    public static GameState RoundTripThroughCanonicalJson(GameState state)
    {
        var json = GameSnapshotSerializer.SerializeState(state);
        var deserialized = GameSnapshotSerializer.DeserializeState(json);
        var reserializedJson = GameSnapshotSerializer.SerializeState(deserialized);

        Assert.Equal(json, reserializedJson);

        return deserialized;
    }

    /// <summary>
    /// Field-by-field structural comparison between an original
    /// <paramref name="expected"/> state and its round-tripped
    /// <paramref name="actual"/> counterpart: territories (owner/troops),
    /// players (troops/eliminated/neutral/headquarters/mission/hand size),
    /// deck size, log count+types, turn flags, and status. Does not replace
    /// <see cref="RoundTripThroughCanonicalJson"/>'s canonical-JSON check —
    /// the two are meant to be used together.
    /// </summary>
    public static void AssertStructurallyEqual(GameState expected, GameState actual)
    {
        Assert.Equal(expected.Territories.Count, actual.Territories.Count);
        foreach (var (territoryId, territoryState) in expected.Territories)
        {
            var roundTripped = actual.Territories[territoryId];
            Assert.Equal(territoryState.Owner, roundTripped.Owner);
            Assert.Equal(territoryState.Troops, roundTripped.Troops);
        }

        Assert.Equal(expected.Players.Count, actual.Players.Count);
        for (var i = 0; i < expected.Players.Count; i++)
        {
            var expectedPlayer = expected.Players[i];
            var actualPlayer = actual.Players[i];

            Assert.Equal(expectedPlayer.Id, actualPlayer.Id);
            Assert.Equal(expectedPlayer.TroopsRemaining, actualPlayer.TroopsRemaining);
            Assert.Equal(expectedPlayer.IsEliminated, actualPlayer.IsEliminated);
            Assert.Equal(expectedPlayer.IsNeutral, actualPlayer.IsNeutral);
            Assert.Equal(expectedPlayer.HeadquartersId, actualPlayer.HeadquartersId);
            Assert.Equal(expectedPlayer.Hand.Count, actualPlayer.Hand.Count);
            AssertMissionEqual(expectedPlayer.Mission, actualPlayer.Mission);
        }

        Assert.Equal(expected.Deck.Count, actual.Deck.Count);

        Assert.Equal(expected.Log.Count, actual.Log.Count);
        for (var i = 0; i < expected.Log.Count; i++)
        {
            Assert.Equal(expected.Log[i].GetType(), actual.Log[i].GetType());
        }

        Assert.Equal(expected.Turn.CurrentPlayer, actual.Turn.CurrentPlayer);
        Assert.Equal(expected.Turn.Phase, actual.Turn.Phase);
        Assert.Equal(expected.Turn.ConqueredThisTurn, actual.Turn.ConqueredThisTurn);
        Assert.Equal(expected.Turn.FortifyUsed, actual.Turn.FortifyUsed);
        Assert.Equal(expected.Turn.MandatoryTradeDown, actual.Turn.MandatoryTradeDown);
        Assert.Equal(expected.Turn.PendingOccupation, actual.Turn.PendingOccupation);

        Assert.Equal(expected.Status.GetType(), actual.Status.GetType());
        if (expected.Status is GameStatus.Won expectedWon)
        {
            var actualWon = Assert.IsType<GameStatus.Won>(actual.Status);
            Assert.Equal(expectedWon.Winner, actualWon.Winner);
        }

        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.TradesCompleted, actual.TradesCompleted);
    }

    private static void AssertMissionEqual(MissionCard? expected, MissionCard? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return;
        }

        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual!.GetType());

        switch (expected)
        {
            case ConquerContinents e:
                var conquer = Assert.IsType<ConquerContinents>(actual);
                Assert.Equal(e.Required, conquer.Required);
                Assert.Equal(e.WildcardCount, conquer.WildcardCount);
                break;
            case EliminateArmy e:
                var eliminate = Assert.IsType<EliminateArmy>(actual);
                Assert.Equal(e.Army, eliminate.Army);
                break;
            case OccupyTerritories e:
                var occupy = Assert.IsType<OccupyTerritories>(actual);
                Assert.Equal(e.Count, occupy.Count);
                Assert.Equal(e.MinArmiesPerTerritory, occupy.MinArmiesPerTerritory);
                break;
        }
    }
}
