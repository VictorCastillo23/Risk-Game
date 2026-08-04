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

public class EliminationTests
{
    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId NorthwestTerritory = new("NorthwestTerritory"); // adjacent to Alaska

    [Fact]
    public void Execute_eliminates_a_player_who_loses_their_last_territory_and_transfers_their_cards()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        IReadOnlyList<Card> defenderHand = [Card(1), Card(2)];
        var state = BuildLastTerritoryAttackState(attacker, defender, attackerHand: [], defenderHand: defenderHand);
        var dice = new QueuedDiceRoller().Enqueue(6).Enqueue(1);
        var engine = new GameEngine(dice);

        var result = engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var eliminatedDefender = ok.State.Players.Single(p => p.Id == defender);
        Assert.True(eliminatedDefender.IsEliminated);
        Assert.Empty(eliminatedDefender.Hand);

        var enrichedAttacker = ok.State.Players.Single(p => p.Id == attacker);
        Assert.Equal(2, enrichedAttacker.Hand.Count);
        Assert.Contains(defenderHand[0], enrichedAttacker.Hand);
        Assert.Contains(defenderHand[1], enrichedAttacker.Hand);

        var eliminatedEvent = Assert.Single(ok.Events.OfType<PlayerEliminated>());
        Assert.Equal(defender, eliminatedEvent.Victim);
        Assert.Equal(attacker, eliminatedEvent.By);
        Assert.Equal(2, eliminatedEvent.CardsTransferred);
    }

    [Fact]
    public void Execute_forces_an_immediate_trade_down_once_the_occupation_resolves_when_elimination_pushes_the_eliminator_to_six_or_more_cards()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        IReadOnlyList<Card> attackerHand = [Card(1), Card(2), Card(3), Card(4)];
        IReadOnlyList<Card> defenderHand = [Card(5), Card(6), Card(7)];
        var state = BuildLastTerritoryAttackState(attacker, defender, attackerHand, defenderHand);
        var dice = new QueuedDiceRoller().Enqueue(6).Enqueue(1);
        var engine = new GameEngine(dice);

        var attackResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, 1)));
        var attackerAfterElimination = attackResult.State.Players.Single(p => p.Id == attacker);
        Assert.Equal(7, attackerAfterElimination.Hand.Count); // 4 + 3 transferred

        // PendingOccupation still takes priority: resolving it is allowed
        // even though the mandatory-trade gate would otherwise also apply.
        var occupyResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(attackResult.State, new OccupyCommand(attacker, 1)));
        Assert.Null(occupyResult.State.Turn.PendingOccupation);

        // Now that the occupation is resolved, the mandatory-trade gate
        // blocks any further command until the eliminator trades down.
        var blocked = engine.Execute(occupyResult.State, new EndPhaseCommand(attacker));
        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(blocked);
        Assert.Equal(GameErrorCode.MandatoryTradeRequired, rejection.Error.Code);
    }

    [Fact]
    public void AdvanceToNextPlayer_skips_an_eliminated_player_when_rotating_the_turn()
    {
        var actor = new PlayerId(0);
        var eliminated = new PlayerId(1);
        var thirdPlayer = new PlayerId(2);
        var territories = WorldMap.Territories.ToDictionary(t => t.Id, t => new TerritoryState(actor, 1));

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(actor, [], false, 0),
            new PlayerState(eliminated, [], true, 0),
            new PlayerState(thirdPlayer, [], false, 0)
        ];

        var state = new GameState(
            territories, players, new TurnState(actor, TurnPhase.Fortify), Deck.CreateStandard(), [], new GameStatus.InProgress());
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(engine.Execute(state, new EndPhaseCommand(actor)));

        Assert.Equal(thirdPlayer, result.State.Turn.CurrentPlayer);
    }

    private static TerritoryCard Card(int territoryIndex) =>
        new(WorldMap.Territories[territoryIndex + 10].Id, WorldMap.Territories[territoryIndex + 10].Symbol);

    /// <summary>
    /// Builds a state where <paramref name="defender"/> owns exactly
    /// <see cref="NorthwestTerritory"/> (their last territory) and
    /// <paramref name="attacker"/> owns every other territory, positioned to
    /// win the battle and eliminate the defender in one dice roll.
    /// </summary>
    /// <summary>
    /// Builds a state where <paramref name="defender"/> owns exactly
    /// <see cref="NorthwestTerritory"/> (their last territory), a third
    /// bystander player owns one unrelated territory (Brazil, so the
    /// attacker does NOT end up owning all 42 territories, keeping this
    /// scenario a pure elimination and not also a simultaneous victory),
    /// and <paramref name="attacker"/> owns everything else.
    /// </summary>
    private static GameState BuildLastTerritoryAttackState(
        PlayerId attacker, PlayerId defender, IReadOnlyList<Card> attackerHand, IReadOnlyList<Card> defenderHand)
    {
        var bystander = new PlayerId(2);
        var bystanderTerritory = new TerritoryId("Brazil");
        var territories = new Dictionary<TerritoryId, TerritoryState>();
        foreach (var territory in WorldMap.Territories)
        {
            territories[territory.Id] = territory.Id == NorthwestTerritory
                ? new TerritoryState(defender, 1)
                : territory.Id == bystanderTerritory
                    ? new TerritoryState(bystander, 1)
                    : new TerritoryState(attacker, 1);
        }
        territories[Alaska] = new TerritoryState(attacker, 4);

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(attacker, attackerHand, false, 0),
            new PlayerState(defender, defenderHand, false, 0),
            new PlayerState(bystander, [], false, 0)
        ];

        return new GameState(
            territories, players, new TurnState(attacker, TurnPhase.Attack), Deck.CreateStandard(), [], new GameStatus.InProgress());
    }
}
