using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.State;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

/// <summary>
/// Pure-model coverage for <see cref="PlayerStats"/>, built from
/// hand-constructed <see cref="GameState"/> records — no engine needed.
/// Design decision: eliminated players are omitted (they own zero
/// territories by the time elimination fires, per <c>GameEngine</c>'s own
/// elimination path — territories are always captured one at a time before
/// the last one triggers <c>PlayerEliminated</c>, so a lingering 0/0 row
/// would be redundant clutter); the neutral army (TwoPlayer mode) is
/// INCLUDED, since it owns territories that count toward the board's
/// totals and is already visibly colored on the board via its own
/// synthesized <c>PlayerConfig</c> (<c>GameSessionService.Start</c>).
/// </summary>
public class PlayerStatsTests
{
    private static readonly PlayerId PlayerA = new(0);
    private static readonly PlayerId PlayerB = new(1);
    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId Greenland = new("Greenland");
    private static readonly TerritoryId Ontario = new("Ontario");

    private static GameState BuildState(
        IReadOnlyDictionary<TerritoryId, TerritoryState> territories,
        IReadOnlyList<PlayerState> players) =>
        new(
            Territories: territories,
            Players: players,
            Turn: new TurnState(PlayerA, TurnPhase.Reinforce),
            Deck: [],
            Log: [],
            Status: new GameStatus.InProgress());

    [Fact]
    public void Rows_CorrectTerritoryAndTroopCounts_PerPlayer()
    {
        var state = BuildState(
            new Dictionary<TerritoryId, TerritoryState>
            {
                [Alaska] = new(PlayerA, 3),
                [Greenland] = new(PlayerB, 5)
            },
            [
                new PlayerState(PlayerA, [], IsEliminated: false, TroopsRemaining: 0),
                new PlayerState(PlayerB, [], IsEliminated: false, TroopsRemaining: 0)
            ]);

        var rows = PlayerStats.Rows(state);

        var rowA = Assert.Single(rows, r => r.Player == PlayerA);
        Assert.Equal(1, rowA.TerritoryCount);
        Assert.Equal(3, rowA.TroopCount);

        var rowB = Assert.Single(rows, r => r.Player == PlayerB);
        Assert.Equal(1, rowB.TerritoryCount);
        Assert.Equal(5, rowB.TroopCount);
    }

    [Fact]
    public void Rows_PlayerWithZeroTerritories_ShowsZeroZero_NotOmitted()
    {
        var state = BuildState(
            new Dictionary<TerritoryId, TerritoryState>
            {
                [Alaska] = new(PlayerA, 3)
            },
            [
                new PlayerState(PlayerA, [], IsEliminated: false, TroopsRemaining: 0),
                new PlayerState(PlayerB, [], IsEliminated: false, TroopsRemaining: 0)
            ]);

        var rows = PlayerStats.Rows(state);

        var rowB = Assert.Single(rows, r => r.Player == PlayerB);
        Assert.Equal(0, rowB.TerritoryCount);
        Assert.Equal(0, rowB.TroopCount);
    }

    [Fact]
    public void Rows_SumsTroopsAcrossMultipleTerritories()
    {
        var state = BuildState(
            new Dictionary<TerritoryId, TerritoryState>
            {
                [Alaska] = new(PlayerA, 3),
                [Greenland] = new(PlayerA, 4),
                [Ontario] = new(PlayerA, 2)
            },
            [
                new PlayerState(PlayerA, [], IsEliminated: false, TroopsRemaining: 0)
            ]);

        var rows = PlayerStats.Rows(state);

        var rowA = Assert.Single(rows);
        Assert.Equal(3, rowA.TerritoryCount);
        Assert.Equal(9, rowA.TroopCount);
    }

    [Fact]
    public void Rows_UnclaimedTerritories_NotAttributedToAnyone()
    {
        var state = BuildState(
            new Dictionary<TerritoryId, TerritoryState>
            {
                [Alaska] = new(PlayerA, 3),
                [Greenland] = new(null, 0),
                [Ontario] = new(null, 0)
            },
            [
                new PlayerState(PlayerA, [], IsEliminated: false, TroopsRemaining: 0)
            ]);

        var rows = PlayerStats.Rows(state);

        var rowA = Assert.Single(rows);
        Assert.Equal(1, rowA.TerritoryCount);
        Assert.Equal(3, rowA.TroopCount);
    }

    [Fact]
    public void Rows_OmitsEliminatedPlayers()
    {
        var state = BuildState(
            new Dictionary<TerritoryId, TerritoryState>
            {
                [Alaska] = new(PlayerA, 3)
            },
            [
                new PlayerState(PlayerA, [], IsEliminated: false, TroopsRemaining: 0),
                new PlayerState(PlayerB, [], IsEliminated: true, TroopsRemaining: 0)
            ]);

        var rows = PlayerStats.Rows(state);

        Assert.Single(rows);
        Assert.DoesNotContain(rows, r => r.Player == PlayerB);
    }

    [Fact]
    public void Rows_IncludesNeutralPlayer()
    {
        var neutral = new PlayerId(2);
        var state = BuildState(
            new Dictionary<TerritoryId, TerritoryState>
            {
                [Alaska] = new(PlayerA, 3),
                [Greenland] = new(neutral, 2)
            },
            [
                new PlayerState(PlayerA, [], IsEliminated: false, TroopsRemaining: 0),
                new PlayerState(neutral, [], IsEliminated: false, TroopsRemaining: 0, IsNeutral: true)
            ]);

        var rows = PlayerStats.Rows(state);

        var neutralRow = Assert.Single(rows, r => r.Player == neutral);
        Assert.Equal(1, neutralRow.TerritoryCount);
        Assert.Equal(2, neutralRow.TroopCount);
    }
}
