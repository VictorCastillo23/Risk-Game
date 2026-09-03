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

public class MandatoryTradeTests
{
    private static readonly TerritoryCard Infantry1 = new(new TerritoryId("Alaska"), CardSymbol.Infantry);
    private static readonly TerritoryCard Infantry2 = new(new TerritoryId("Alberta"), CardSymbol.Infantry);
    private static readonly TerritoryCard Infantry3 = new(new TerritoryId("Ontario"), CardSymbol.Infantry);
    private static readonly TerritoryCard Infantry4 = new(new TerritoryId("Quebec"), CardSymbol.Infantry);
    private static readonly TerritoryCard Infantry5 = new(new TerritoryId("Greenland"), CardSymbol.Infantry);

    [Fact]
    public void Execute_rejects_reinforcement_when_the_actor_holds_five_or_more_cards()
    {
        var actor = new PlayerId(0);
        IReadOnlyList<Card> hand = [Infantry1, Infantry2, Infantry3, Infantry4, Infantry5];
        var state = BuildReinforceReadyState(actor, hand, troopsRemaining: 3);
        var engine = new GameEngine(new QueuedDiceRoller());
        var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;

        var result = engine.Execute(state, new PlaceTroopsCommand(actor, territory, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.MandatoryTradeRequired, rejection.Error.Code);
    }

    [Fact]
    public void Execute_allows_trading_while_the_mandatory_trade_gate_is_active()
    {
        var actor = new PlayerId(0);
        IReadOnlyList<Card> hand = [Infantry1, Infantry2, Infantry3, Infantry4, Infantry5];
        var state = BuildReinforceReadyState(actor, hand, troopsRemaining: 3);
        var engine = new GameEngine(new QueuedDiceRoller());

        // Actor owns every territory in this fixture, so all 3 traded cards
        // name owned territories: the bonus territory choice is required.
        var result = engine.Execute(state, new TradeCardsCommand(actor, [Infantry1, Infantry2, Infantry3], Infantry1.Territory));

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
    }

    [Fact]
    public void Execute_unblocks_reinforcement_once_the_hand_drops_below_five_cards()
    {
        var actor = new PlayerId(0);
        IReadOnlyList<Card> hand = [Infantry1, Infantry2, Infantry3, Infantry4, Infantry5];
        var state = BuildReinforceReadyState(actor, hand, troopsRemaining: 3);
        var engine = new GameEngine(new QueuedDiceRoller());
        var traded = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new TradeCardsCommand(actor, [Infantry1, Infantry2, Infantry3], Infantry1.Territory)));
        var territory = traded.State.Territories.First(kv => kv.Value.Owner == actor).Key;

        var result = engine.Execute(traded.State, new PlaceTroopsCommand(actor, territory, 1));

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
    }

    [Fact]
    public void Execute_allows_OccupyCommand_even_while_the_mandatory_trade_flag_is_armed()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        IReadOnlyList<Card> hand = [Infantry1, Infantry2, Infantry3, Infantry4, Infantry5];
        var territories = WorldMap.Territories.ToDictionary(t => t.Id, t => new TerritoryState(actor, 1));
        var from = new TerritoryId("Alaska");
        var conquered = new TerritoryId("NorthwestTerritory");
        territories[from] = new TerritoryState(actor, 3);
        territories[conquered] = new TerritoryState(actor, 0);

        var pending = new PendingOccupation(from, conquered, 1);
        var turn = new TurnState(actor, TurnPhase.Attack, PendingOccupation: pending, MandatoryTradeDown: true);

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(actor, hand, false, 0),
            new PlayerState(other, [], false, 0)
        ];

        var state = new GameState(territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress());
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new OccupyCommand(actor, 1));

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
    }

    private static GameState BuildReinforceReadyState(PlayerId actor, IReadOnlyList<Card> hand, int troopsRemaining)
    {
        var other = new PlayerId(actor.Value + 1);
        var territories = WorldMap.Territories.ToDictionary(t => t.Id, t => new TerritoryState(actor, 1));

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(actor, hand, false, troopsRemaining),
            new PlayerState(other, [], false, 0)
        ];

        return new GameState(
            territories,
            players,
            new TurnState(actor, TurnPhase.Reinforce),
            Deck.CreateStandard(),
            [],
            new GameStatus.InProgress());
    }
}
