using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.State;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class BoardSelectionTests
{
    private static readonly PlayerId Player0 = new(0);
    private static readonly PlayerId Player1 = new(1);

    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId Alberta = new("Alberta");
    private static readonly TerritoryId Ontario = new("Ontario");
    private static readonly TerritoryId Quebec = new("Quebec");

    /// <summary>
    /// Builds a minimal <see cref="GameState"/> with every <see cref="WorldMap"/>
    /// territory defaulted to <paramref name="defaultOwner"/>, except the
    /// entries in <paramref name="owners"/>, which win.
    /// </summary>
    private static GameState BuildState(
        TurnPhase phase, PlayerId currentPlayer, IReadOnlyDictionary<TerritoryId, PlayerId> owners, PlayerId defaultOwner)
    {
        var territories = WorldMap.Territories.ToDictionary(
            t => t.Id,
            t => new TerritoryState(owners.GetValueOrDefault(t.Id, defaultOwner), 5));

        return new GameState(
            territories,
            [new PlayerState(Player0, [], false, 0), new PlayerState(Player1, [], false, 0)],
            new TurnState(currentPlayer, phase),
            [],
            [],
            new GameStatus.InProgress());
    }

    // --- Reinforce: single-territory selection ---

    [Fact]
    public void Click_DuringReinforce_OnOwnTerritory_SetsOriginAsSelection()
    {
        var state = BuildState(TurnPhase.Reinforce, Player0, new Dictionary<TerritoryId, PlayerId> { [Alaska] = Player0 }, Player1);

        var selection = BoardSelection.Empty.Click(Alaska, state, Player0, TurnPhase.Reinforce);

        Assert.Equal(Alaska, selection.Origin);
        Assert.Null(selection.Destination);
    }

    [Fact]
    public void Click_DuringReinforce_OnEnemyTerritory_LeavesSelectionUnchanged()
    {
        var state = BuildState(TurnPhase.Reinforce, Player0, new Dictionary<TerritoryId, PlayerId> { [Alberta] = Player0 }, Player1);
        var selected = BoardSelection.Empty.Click(Alberta, state, Player0, TurnPhase.Reinforce);

        var result = selected.Click(Alaska, state, Player0, TurnPhase.Reinforce);

        Assert.Equal(selected, result);
    }

    // --- Attack: own then adjacent-enemy ---

    [Fact]
    public void Click_DuringAttack_OwnThenAdjacentEnemy_SetsDestination()
    {
        var owners = new Dictionary<TerritoryId, PlayerId> { [Alaska] = Player0, [Alberta] = Player1 };
        var state = BuildState(TurnPhase.Attack, Player0, owners, Player1);

        var afterOrigin = BoardSelection.Empty.Click(Alaska, state, Player0, TurnPhase.Attack);
        var afterDestination = afterOrigin.Click(Alberta, state, Player0, TurnPhase.Attack);

        Assert.Equal(Alaska, afterDestination.Origin);
        Assert.Equal(Alberta, afterDestination.Destination);
    }

    [Fact]
    public void Click_DuringAttack_OwnThenNonAdjacentEnemy_DoesNotBuildValidAttackPair()
    {
        var owners = new Dictionary<TerritoryId, PlayerId> { [Alaska] = Player0, [Quebec] = Player1 };
        var state = BuildState(TurnPhase.Attack, Player0, owners, Player1);

        var afterOrigin = BoardSelection.Empty.Click(Alaska, state, Player0, TurnPhase.Attack);
        var result = afterOrigin.Click(Quebec, state, Player0, TurnPhase.Attack);

        Assert.Equal(Alaska, result.Origin);
        Assert.Null(result.Destination);
    }

    [Fact]
    public void Click_DuringAttack_OwnThenAnotherOwnTerritory_ReselectsOrigin()
    {
        var owners = new Dictionary<TerritoryId, PlayerId> { [Alaska] = Player0, [Ontario] = Player0 };
        var state = BuildState(TurnPhase.Attack, Player0, owners, Player1);

        var afterOrigin = BoardSelection.Empty.Click(Alaska, state, Player0, TurnPhase.Attack);
        var result = afterOrigin.Click(Ontario, state, Player0, TurnPhase.Attack);

        Assert.Equal(Ontario, result.Origin);
        Assert.Null(result.Destination);
    }

    /// <summary>
    /// Regression/approval test for a near-miss caught during PR4 TDD: an
    /// earlier draft of Attack's <c>isValidDestination</c> checked only
    /// <c>WorldMap.AreAdjacent</c> without an explicit <c>!isOwn</c> guard,
    /// which would have let an adjacent *own* territory silently complete as
    /// a bogus attack pair. The PR4 fixture (Alaska/Ontario) happened to be
    /// non-adjacent, so it could not have caught that bug — this uses
    /// Alaska/Alberta, which ARE adjacent, to close that gap. Currently
    /// passing because the fix already shipped in PR4; kept as a standing
    /// guard against the bug being reintroduced.
    /// </summary>
    [Fact]
    public void Click_DuringAttack_OwnThenAdjacentOwnTerritory_ReselectsOriginInsteadOfCompletingAttack()
    {
        var owners = new Dictionary<TerritoryId, PlayerId> { [Alaska] = Player0, [Alberta] = Player0 };
        var state = BuildState(TurnPhase.Attack, Player0, owners, Player1);

        var afterOrigin = BoardSelection.Empty.Click(Alaska, state, Player0, TurnPhase.Attack);
        var result = afterOrigin.Click(Alberta, state, Player0, TurnPhase.Attack);

        Assert.Equal(Alberta, result.Origin);
        Assert.Null(result.Destination);
    }

    // --- Fortify: own then friendly-connected own (real BFS, not direct adjacency) ---

    [Fact]
    public void Click_DuringFortify_OwnThenIndirectlyConnectedOwn_SetsDestination()
    {
        // Alaska -> Alberta -> Ontario -> Quebec, all owned by Player0: no direct
        // Alaska-Quebec adjacency exists, so this only passes with a real path search.
        var owners = new Dictionary<TerritoryId, PlayerId>
        {
            [Alaska] = Player0, [Alberta] = Player0, [Ontario] = Player0, [Quebec] = Player0
        };
        var state = BuildState(TurnPhase.Fortify, Player0, owners, Player1);

        var afterOrigin = BoardSelection.Empty.Click(Alaska, state, Player0, TurnPhase.Fortify);
        var result = afterOrigin.Click(Quebec, state, Player0, TurnPhase.Fortify);

        Assert.Equal(Alaska, result.Origin);
        Assert.Equal(Quebec, result.Destination);
    }

    [Fact]
    public void Click_DuringFortify_OwnButDisconnectedOwn_ReselectsOriginInsteadOfCompleting()
    {
        // Alaska's only neighbors (Alberta, NorthwestTerritory, Kamchatka) are all
        // enemy-owned, so Alaska has no friendly path to Ontario despite both
        // being owned by Player0.
        var owners = new Dictionary<TerritoryId, PlayerId> { [Alaska] = Player0, [Ontario] = Player0 };
        var state = BuildState(TurnPhase.Fortify, Player0, owners, Player1);

        var afterOrigin = BoardSelection.Empty.Click(Alaska, state, Player0, TurnPhase.Fortify);
        var result = afterOrigin.Click(Ontario, state, Player0, TurnPhase.Fortify);

        Assert.Equal(Ontario, result.Origin);
        Assert.Null(result.Destination);
    }

    [Fact]
    public void Click_DuringFortify_OnEnemyTerritory_LeavesSelectionUnchanged()
    {
        var owners = new Dictionary<TerritoryId, PlayerId> { [Alaska] = Player0, [Alberta] = Player1 };
        var state = BuildState(TurnPhase.Fortify, Player0, owners, Player1);

        var afterOrigin = BoardSelection.Empty.Click(Alaska, state, Player0, TurnPhase.Fortify);
        var result = afterOrigin.Click(Alberta, state, Player0, TurnPhase.Fortify);

        Assert.Equal(afterOrigin, result);
    }

    // --- Shared behavior ---

    [Fact]
    public void Clear_ResetsToEmptySelection()
    {
        var state = BuildState(TurnPhase.Reinforce, Player0, new Dictionary<TerritoryId, PlayerId> { [Alaska] = Player0 }, Player1);
        var selected = BoardSelection.Empty.Click(Alaska, state, Player0, TurnPhase.Reinforce);

        var cleared = selected.Clear();

        Assert.Equal(BoardSelection.Empty, cleared);
    }
}
