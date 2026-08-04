using Risk.Domain.Cards;
using Risk.Domain.Errors;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

public class FortifyCommandTests
{
    // NA chain used to build connectivity scenarios:
    // Alaska -- NorthwestTerritory -- Alberta -- Ontario -- Quebec
    // Alaska is NOT directly adjacent to Ontario or Quebec.
    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId NorthwestTerritory = new("NorthwestTerritory");
    private static readonly TerritoryId Alberta = new("Alberta");
    private static readonly TerritoryId Ontario = new("Ontario");
    private static readonly TerritoryId Quebec = new("Quebec");

    // The only edge connecting North America and South America in the world
    // map is CentralAmerica <-> Venezuela, making CentralAmerica a natural
    // single-point-of-failure chokepoint for a "path blocked by an enemy
    // territory" scenario.
    private static readonly TerritoryId WesternUnitedStates = new("WesternUnitedStates");
    private static readonly TerritoryId CentralAmerica = new("CentralAmerica");
    private static readonly TerritoryId Venezuela = new("Venezuela");

    [Fact]
    public void Execute_moves_troops_when_source_and_destination_are_connected_by_a_chain_of_owned_territories()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildFortifyReadyState(
            actor,
            owned: [Alaska, NorthwestTerritory, Alberta, Ontario, Quebec],
            troopsByTerritory: new Dictionary<TerritoryId, int> { [Alaska] = 5, [Quebec] = 1 },
            other);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new FortifyCommand(actor, Alaska, Quebec, 3));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(2, ok.State.Territories[Alaska].Troops);
        Assert.Equal(4, ok.State.Territories[Quebec].Troops);
        Assert.True(ok.State.Turn.FortifyUsed);
    }

    [Fact]
    public void Execute_rejects_fortify_when_the_only_path_runs_through_an_enemy_owned_territory()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        // WesternUnitedStates and Venezuela are both owned by the actor, but
        // every other territory (including the sole connecting territory,
        // CentralAmerica) belongs to the enemy, so no all-owned chain exists
        // between them.
        var state = BuildFortifyReadyState(
            actor,
            owned: [WesternUnitedStates, Venezuela],
            troopsByTerritory: new Dictionary<TerritoryId, int> { [WesternUnitedStates] = 5, [Venezuela] = 1 },
            other);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new FortifyCommand(actor, WesternUnitedStates, Venezuela, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NoFriendlyPath, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_fortify_when_the_destination_is_directly_adjacent_but_not_owned_by_the_actor()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        // Alaska and NorthwestTerritory are directly adjacent, but the
        // destination is owned by the enemy (the default for every
        // unlisted territory): direct physical adjacency alone must not
        // substitute for the ownership check.
        var state = BuildFortifyReadyState(
            actor,
            owned: [Alaska],
            troopsByTerritory: new Dictionary<TerritoryId, int> { [Alaska] = 5 },
            other);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new FortifyCommand(actor, Alaska, NorthwestTerritory, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotOwner, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_fortify_that_would_leave_no_troops_behind_in_the_source_territory()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildFortifyReadyState(
            actor,
            owned: [Alaska, NorthwestTerritory],
            troopsByTerritory: new Dictionary<TerritoryId, int> { [Alaska] = 3, [NorthwestTerritory] = 1 },
            other);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new FortifyCommand(actor, Alaska, NorthwestTerritory, 3));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidTroopCount, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_a_second_fortify_in_the_same_turn()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildFortifyReadyState(
            actor,
            owned: [Alaska, NorthwestTerritory],
            troopsByTerritory: new Dictionary<TerritoryId, int> { [Alaska] = 5, [NorthwestTerritory] = 1 },
            other,
            fortifyUsed: true);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new FortifyCommand(actor, Alaska, NorthwestTerritory, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.FortifyAlreadyUsed, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_fortify_issued_outside_the_fortify_phase()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildFortifyReadyState(
            actor,
            owned: [Alaska, NorthwestTerritory],
            troopsByTerritory: new Dictionary<TerritoryId, int> { [Alaska] = 5, [NorthwestTerritory] = 1 },
            other,
            phase: TurnPhase.Attack);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new FortifyCommand(actor, Alaska, NorthwestTerritory, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.WrongPhase, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_fortify_from_a_player_who_is_not_the_active_player()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildFortifyReadyState(
            actor,
            owned: [Alaska, NorthwestTerritory],
            troopsByTerritory: new Dictionary<TerritoryId, int> { [Alaska] = 5, [NorthwestTerritory] = 1 },
            other);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new FortifyCommand(other, Alaska, NorthwestTerritory, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotYourTurn, rejection.Error.Code);
    }

    /// <summary>
    /// Builds a minimal 2-player, Fortify-phase (by default) state where
    /// only the territories in <paramref name="owned"/> belong to
    /// <paramref name="currentPlayer"/> (troop counts from
    /// <paramref name="troopsByTerritory"/>, default 1); every other
    /// territory on the map is owned by <paramref name="otherPlayer"/>, so
    /// connectivity tests only "see" the territories the test explicitly
    /// grants to the actor.
    /// </summary>
    private static GameState BuildFortifyReadyState(
        PlayerId currentPlayer,
        IReadOnlyList<TerritoryId> owned,
        IReadOnlyDictionary<TerritoryId, int> troopsByTerritory,
        PlayerId otherPlayer,
        bool fortifyUsed = false,
        TurnPhase phase = TurnPhase.Fortify)
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>();

        foreach (var territory in WorldMap.Territories)
        {
            var owner = owned.Contains(territory.Id) ? currentPlayer : otherPlayer;
            var troops = troopsByTerritory.TryGetValue(territory.Id, out var explicitTroops) ? explicitTroops : 1;
            territories[territory.Id] = new TerritoryState(owner, troops);
        }

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(currentPlayer, [], false, 0),
            new PlayerState(otherPlayer, [], false, 0)
        ];

        var turn = new TurnState(currentPlayer, phase, FortifyUsed: fortifyUsed);

        return new GameState(territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress());
    }
}
