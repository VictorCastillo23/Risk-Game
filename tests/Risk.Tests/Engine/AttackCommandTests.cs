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

public class AttackCommandTests
{
    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId NorthwestTerritory = new("NorthwestTerritory"); // adjacent to Alaska
    private static readonly TerritoryId Brazil = new("Brazil"); // NOT adjacent to Alaska

    [Fact]
    public void Execute_rejects_an_attack_against_a_non_adjacent_territory()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        var state = BuildAttackReadyState(attacker, Alaska, 3, attacker, Brazil, 1, defender);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new AttackCommand(attacker, Alaska, Brazil, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotAdjacent, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_an_attack_when_the_actor_does_not_own_the_source_territory()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        // Source (Alaska) is owned by the defender, not the attacker.
        var state = BuildAttackReadyState(attacker, Alaska, 3, defender, NorthwestTerritory, 1, defender);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotOwner, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_an_attack_when_the_actor_already_owns_the_target_territory()
    {
        var attacker = new PlayerId(0);
        var state = BuildAttackReadyState(attacker, Alaska, 3, attacker, NorthwestTerritory, 1, attacker);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotOwner, rejection.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Execute_rejects_an_attack_with_a_dice_count_outside_one_to_three(int diceCount)
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        var state = BuildAttackReadyState(attacker, Alaska, 5, attacker, NorthwestTerritory, 1, defender);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, diceCount));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidDiceCount, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_an_attack_that_would_leave_no_troops_behind_in_the_source_territory()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        // Only 3 troops in the source: rolling all 3 as attack dice would
        // leave 0 troops behind, which must be rejected.
        var state = BuildAttackReadyState(attacker, Alaska, 3, attacker, NorthwestTerritory, 1, defender);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, 3));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InsufficientTroops, rejection.Error.Code);
    }

    /// <summary>
    /// Builds a minimal 2-player, Attack-phase state for <paramref name="currentPlayer"/>
    /// where every territory other than the two under test is a harmless
    /// filler owned by <paramref name="currentPlayer"/> (combat logic only
    /// ever reads/writes <paramref name="from"/> and <paramref name="to"/>).
    /// </summary>
    internal static GameState BuildAttackReadyState(
        PlayerId currentPlayer,
        TerritoryId from, int fromTroops, PlayerId fromOwner,
        TerritoryId to, int toTroops, PlayerId toOwner)
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>();

        foreach (var territory in WorldMap.Territories)
        {
            territories[territory.Id] = territory.Id == from
                ? new TerritoryState(fromOwner, fromTroops)
                : territory.Id == to
                    ? new TerritoryState(toOwner, toTroops)
                    : new TerritoryState(currentPlayer, 1);
        }

        var otherPlayer = fromOwner != currentPlayer ? fromOwner : toOwner != currentPlayer ? toOwner : new PlayerId(currentPlayer.Value + 1);

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(currentPlayer, [], false, 0),
            new PlayerState(otherPlayer, [], false, 0)
        ];

        return new GameState(
            territories,
            players,
            new TurnState(currentPlayer, TurnPhase.Attack),
            Deck.CreateStandard(),
            [],
            new GameStatus.InProgress());
    }
}
