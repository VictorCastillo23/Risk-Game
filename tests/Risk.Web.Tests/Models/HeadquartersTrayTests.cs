using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.State;
using Risk.Engine.Views;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

/// <summary>
/// Pure-model coverage for <see cref="HeadquartersTray"/> (design D2), built
/// from hand-constructed <see cref="PlayerView"/> records — no engine needed.
/// The "anti-leak" scenarios here are the ones the whole change is built
/// around (per the critical constraint): <see cref="IsRevealed"/> MUST stay
/// false for every intermediate state of the pick phase, not only before
/// anyone has picked.
/// </summary>
public class HeadquartersTrayTests
{
    private static readonly PlayerId PlayerA = new(0);
    private static readonly PlayerId PlayerB = new(1);
    private static readonly PlayerId PlayerC = new(2);
    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId Greenland = new("Greenland");
    private static readonly TerritoryId Ontario = new("Ontario");

    private static PlayerView BuildView(
        IReadOnlyDictionary<TerritoryId, TerritoryState> territories,
        TerritoryId? ownHeadquarters = null,
        IReadOnlyDictionary<PlayerId, TerritoryId>? revealedHeadquarters = null) =>
        new(
            Territories: territories,
            OwnHand: [],
            OtherPlayersCardCounts: new Dictionary<PlayerId, int>(),
            Turn: new TurnState(PlayerA, TurnPhase.SelectHeadquarters),
            OwnHeadquarters: ownHeadquarters,
            RevealedHeadquarters: revealedHeadquarters ?? new Dictionary<PlayerId, TerritoryId>(),
            OwnEffectiveMission: null);

    [Fact]
    public void IsRevealed_False_BeforeAnyoneHasPicked()
    {
        var view = BuildView(new Dictionary<TerritoryId, TerritoryState> { [Alaska] = new(PlayerA, 3) });

        Assert.False(HeadquartersTray.IsRevealed(view));
    }

    /// <summary>
    /// Critical anti-leak scenario: the viewer has already picked their own
    /// HQ (<c>OwnHeadquarters</c> is set), but not every player has picked
    /// yet (<c>RevealedHeadquarters</c> stays empty per design D1's
    /// monotonic derivation) — <see cref="HeadquartersTray.IsRevealed"/> MUST
    /// still be false. This is the exact intermediate step a mode-only or
    /// "picker sees their own" gate would get wrong.
    /// </summary>
    [Fact]
    public void IsRevealed_False_AfterSomeButNotAllPlayersHavePicked()
    {
        var view = BuildView(
            new Dictionary<TerritoryId, TerritoryState> { [Alaska] = new(PlayerA, 3) },
            ownHeadquarters: Alaska,
            revealedHeadquarters: new Dictionary<PlayerId, TerritoryId>());

        Assert.False(HeadquartersTray.IsRevealed(view));
    }

    [Fact]
    public void IsRevealed_False_OutsideCapital()
    {
        // Classic/SecretMission/TwoPlayer views always carry a null
        // OwnHeadquarters and an empty RevealedHeadquarters — indistinguishable
        // at this seam from "Capital, pre-reveal", which is exactly the point
        // (design D1: Count > 0 alone encodes "Capital AND revealed").
        var view = BuildView(new Dictionary<TerritoryId, TerritoryState> { [Alaska] = new(PlayerA, 3) });

        Assert.False(HeadquartersTray.IsRevealed(view));
    }

    [Fact]
    public void IsRevealed_True_AfterEveryoneHasPicked()
    {
        var revealed = new Dictionary<PlayerId, TerritoryId> { [PlayerA] = Alaska, [PlayerB] = Greenland };
        var view = BuildView(
            new Dictionary<TerritoryId, TerritoryState>
            {
                [Alaska] = new(PlayerA, 3),
                [Greenland] = new(PlayerB, 2)
            },
            ownHeadquarters: Alaska,
            revealedHeadquarters: revealed);

        Assert.True(HeadquartersTray.IsRevealed(view));
    }

    [Fact]
    public void Rows_Empty_PreReveal()
    {
        var view = BuildView(new Dictionary<TerritoryId, TerritoryState> { [Alaska] = new(PlayerA, 3) });

        Assert.Empty(HeadquartersTray.Rows(view));
    }

    [Fact]
    public void Rows_OneRowPerPlayer_WithCorrectHolder_AfterCapture()
    {
        // Player B captured Player A's HQ (Alaska): the row still says
        // "declared by A", but Holder now reports B — derived from live
        // Territories, not a stored field.
        var revealed = new Dictionary<PlayerId, TerritoryId> { [PlayerA] = Alaska, [PlayerB] = Greenland, [PlayerC] = Ontario };
        var view = BuildView(
            new Dictionary<TerritoryId, TerritoryState>
            {
                [Alaska] = new(PlayerB, 4), // captured
                [Greenland] = new(PlayerB, 2),
                [Ontario] = new(PlayerC, 3)
            },
            ownHeadquarters: Greenland,
            revealedHeadquarters: revealed);

        var rows = HeadquartersTray.Rows(view);

        Assert.Equal(3, rows.Count);
        var aliceRow = Assert.Single(rows, r => r.Declarer == PlayerA);
        Assert.Equal(Alaska, aliceRow.Territory);
        Assert.Equal(PlayerB, aliceRow.Holder);

        var bobRow = Assert.Single(rows, r => r.Declarer == PlayerB);
        Assert.Equal(Greenland, bobRow.Territory);
        Assert.Equal(PlayerB, bobRow.Holder);
    }

    [Fact]
    public void HasLostOwnHeadquarters_False_PreReveal_EvenIfCaptured()
    {
        // Structurally impossible pre-reveal (no attack can target a
        // secret HQ), but the gate must not depend on that — IsRevealed
        // false alone must short-circuit this to false.
        var view = BuildView(
            new Dictionary<TerritoryId, TerritoryState> { [Alaska] = new(PlayerB, 3) },
            ownHeadquarters: Alaska);

        Assert.False(HeadquartersTray.HasLostOwnHeadquarters(view, PlayerA));
    }

    [Fact]
    public void HasLostOwnHeadquarters_False_WhileStillHeld()
    {
        var revealed = new Dictionary<PlayerId, TerritoryId> { [PlayerA] = Alaska, [PlayerB] = Greenland };
        var view = BuildView(
            new Dictionary<TerritoryId, TerritoryState>
            {
                [Alaska] = new(PlayerA, 3),
                [Greenland] = new(PlayerB, 2)
            },
            ownHeadquarters: Alaska,
            revealedHeadquarters: revealed);

        Assert.False(HeadquartersTray.HasLostOwnHeadquarters(view, PlayerA));
    }

    [Fact]
    public void HasLostOwnHeadquarters_True_AfterLoss()
    {
        var revealed = new Dictionary<PlayerId, TerritoryId> { [PlayerA] = Alaska, [PlayerB] = Greenland };
        var view = BuildView(
            new Dictionary<TerritoryId, TerritoryState>
            {
                [Alaska] = new(PlayerB, 4), // captured by B
                [Greenland] = new(PlayerB, 2)
            },
            ownHeadquarters: Alaska,
            revealedHeadquarters: revealed);

        Assert.True(HeadquartersTray.HasLostOwnHeadquarters(view, PlayerA));
    }

    [Fact]
    public void HasLostOwnHeadquarters_False_AgainAfterRecapture()
    {
        var revealed = new Dictionary<PlayerId, TerritoryId> { [PlayerA] = Alaska, [PlayerB] = Greenland };
        var view = BuildView(
            new Dictionary<TerritoryId, TerritoryState>
            {
                [Alaska] = new(PlayerA, 5), // recaptured
                [Greenland] = new(PlayerB, 2)
            },
            ownHeadquarters: Alaska,
            revealedHeadquarters: revealed);

        Assert.False(HeadquartersTray.HasLostOwnHeadquarters(view, PlayerA));
    }
}
