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

public class AttackCombatTests
{
    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId NorthwestTerritory = new("NorthwestTerritory"); // adjacent to Alaska

    [Fact]
    public void Execute_applies_troop_losses_to_both_sides_without_conquering()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        var state = AttackCommandTests.BuildAttackReadyState(attacker, Alaska, 4, attacker, NorthwestTerritory, 3, defender);
        var dice = new QueuedDiceRoller().Enqueue(5, 3).Enqueue(5, 2); // spec scenario: tie favors defender
        var engine = new GameEngine(dice);

        var result = engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, 2));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(3, ok.State.Territories[Alaska].Troops); // lost 1 (the tied pair)
        Assert.Equal(2, ok.State.Territories[NorthwestTerritory].Troops); // lost 1 (the 3-vs-2 pair)
        Assert.Equal(defender, ok.State.Territories[NorthwestTerritory].Owner); // no conquest yet
        Assert.Null(ok.State.Turn.PendingOccupation);
        Assert.False(ok.State.Turn.ConqueredThisTurn); // task 5.4: flag only flips on an actual conquest
    }

    [Fact]
    public void Execute_conquers_the_territory_when_the_defenders_troops_reach_zero()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        var state = AttackCommandTests.BuildAttackReadyState(attacker, Alaska, 4, attacker, NorthwestTerritory, 1, defender);
        var dice = new QueuedDiceRoller().Enqueue(6).Enqueue(1);
        var engine = new GameEngine(dice);

        var result = engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var conquered = ok.State.Territories[NorthwestTerritory];
        Assert.Equal(attacker, conquered.Owner);
        Assert.Equal(0, conquered.Troops);
        Assert.True(ok.State.Turn.ConqueredThisTurn);
        var pending = Assert.IsType<PendingOccupation>(ok.State.Turn.PendingOccupation);
        Assert.Equal(Alaska, pending.From);
        Assert.Equal(NorthwestTerritory, pending.Conquered);
        Assert.Equal(1, pending.MinimumTroops); // 1 attacker die used in the winning round
    }

    [Fact]
    public void Execute_supports_multiple_battle_rounds_until_the_defending_territory_is_fully_conquered()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        var state = AttackCommandTests.BuildAttackReadyState(attacker, Alaska, 6, attacker, NorthwestTerritory, 3, defender);
        var dice = new QueuedDiceRoller()
            .Enqueue(6, 6).Enqueue(1, 1)  // round 1: attacker wins both pairs, defender 3 -> 1 troop
            .Enqueue(6).Enqueue(1);       // round 2: attacker wins, defender 1 -> 0 troops, conquered
        var engine = new GameEngine(dice);

        var round1 = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, 2)));
        Assert.Equal(1, round1.State.Territories[NorthwestTerritory].Troops);
        Assert.Equal(defender, round1.State.Territories[NorthwestTerritory].Owner);

        var round2 = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(round1.State, new AttackCommand(attacker, Alaska, NorthwestTerritory, 1)));

        var conquered = round2.State.Territories[NorthwestTerritory];
        Assert.Equal(attacker, conquered.Owner);
        Assert.Equal(0, conquered.Troops);
        Assert.NotNull(round2.State.Turn.PendingOccupation);
        Assert.Equal(1, round2.State.Turn.PendingOccupation!.MinimumTroops);
    }

    [Fact]
    public void Execute_rejects_any_other_command_while_an_occupation_is_pending()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        var state = BuildPendingOccupationState(attacker, Alaska, 4, NorthwestTerritory, minimumTroops: 1, defender);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new PlaceTroopsCommand(attacker, Alaska, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.OccupationPending, rejection.Error.Code);
    }

    [Fact]
    public void Occupy_rejects_a_troop_count_below_the_dice_used_minimum()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        var state = BuildPendingOccupationState(attacker, Alaska, 4, NorthwestTerritory, minimumTroops: 2, defender);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new OccupyCommand(attacker, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidTroopCount, rejection.Error.Code);
    }

    [Fact]
    public void Occupy_rejects_a_troop_count_above_the_attackers_available_troops()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        // Source has 4 troops; max movable leaves at least 1 behind, so 3 is the ceiling.
        var state = BuildPendingOccupationState(attacker, Alaska, 4, NorthwestTerritory, minimumTroops: 1, defender);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new OccupyCommand(attacker, 4));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidTroopCount, rejection.Error.Code);
    }

    [Fact]
    public void Occupy_moves_troops_in_and_clears_the_pending_occupation()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        var state = BuildPendingOccupationState(attacker, Alaska, 4, NorthwestTerritory, minimumTroops: 2, defender);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new OccupyCommand(attacker, 3));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(1, ok.State.Territories[Alaska].Troops); // 4 - 3 moved
        Assert.Equal(3, ok.State.Territories[NorthwestTerritory].Troops);
        Assert.Equal(attacker, ok.State.Territories[NorthwestTerritory].Owner);
        Assert.Null(ok.State.Turn.PendingOccupation);
    }

    /// <summary>
    /// Builds a state where <paramref name="conquered"/> was already
    /// conquered by <paramref name="currentPlayer"/> and is awaiting
    /// occupation, isolating <c>OccupyCommand</c> validation from having to
    /// re-run a full battle every time.
    /// </summary>
    private static GameState BuildPendingOccupationState(
        PlayerId currentPlayer, TerritoryId from, int fromTroops,
        TerritoryId conquered, int minimumTroops, PlayerId otherPlayer)
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>();

        foreach (var territory in WorldMap.Territories)
        {
            territories[territory.Id] = territory.Id == from
                ? new TerritoryState(currentPlayer, fromTroops)
                : territory.Id == conquered
                    ? new TerritoryState(currentPlayer, 0)
                    : new TerritoryState(currentPlayer, 1);
        }

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(currentPlayer, [], false, 0),
            new PlayerState(otherPlayer, [], false, 0)
        ];

        var turn = new TurnState(
            currentPlayer,
            TurnPhase.Attack,
            ConqueredThisTurn: true,
            PendingOccupation: new PendingOccupation(from, conquered, minimumTroops));

        return new GameState(territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress());
    }
}
