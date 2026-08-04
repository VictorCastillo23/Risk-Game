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

public class TradeCardsCommandTests
{
    private static readonly TerritoryCard Infantry1 = new(new TerritoryId("Alaska"), CardSymbol.Infantry);
    private static readonly TerritoryCard Infantry2 = new(new TerritoryId("Alberta"), CardSymbol.Infantry);
    private static readonly TerritoryCard Infantry3 = new(new TerritoryId("Ontario"), CardSymbol.Infantry);
    private static readonly TerritoryCard Cavalry1 = new(new TerritoryId("Brazil"), CardSymbol.Cavalry);

    [Fact]
    public void Execute_trades_a_valid_set_removes_the_cards_and_grants_the_first_trade_bonus()
    {
        var actor = new PlayerId(0);
        IReadOnlyList<Card> hand = [Infantry1, Infantry2, Infantry3];
        var state = BuildTradeReadyState(actor, hand, troopsRemaining: 0, tradesCompleted: 0);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new TradeCardsCommand(actor, hand));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var player = ok.State.Players.Single(p => p.Id == actor);
        Assert.Empty(player.Hand);
        Assert.Equal(4, player.TroopsRemaining); // first trade this game
        Assert.Equal(1, ok.State.TradesCompleted);
        var traded = Assert.Single(ok.Events.OfType<CardsTraded>());
        Assert.Equal(4, traded.Bonus);
    }

    [Fact]
    public void Execute_grants_the_escalating_bonus_for_the_second_trade_this_game()
    {
        var actor = new PlayerId(0);
        IReadOnlyList<Card> hand = [Infantry1, Infantry2, Infantry3];
        var state = BuildTradeReadyState(actor, hand, troopsRemaining: 0, tradesCompleted: 1);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new TradeCardsCommand(actor, hand));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var player = ok.State.Players.Single(p => p.Id == actor);
        Assert.Equal(6, player.TroopsRemaining); // second trade this game
        Assert.Equal(2, ok.State.TradesCompleted);
    }

    [Fact]
    public void Execute_rejects_a_set_that_is_not_a_valid_combination()
    {
        var actor = new PlayerId(0);
        IReadOnlyList<Card> hand = [Infantry1, Infantry2, Cavalry1];
        var state = BuildTradeReadyState(actor, hand, troopsRemaining: 0, tradesCompleted: 0);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new TradeCardsCommand(actor, hand));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidCardSet, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_a_trade_with_cards_the_player_does_not_hold()
    {
        var actor = new PlayerId(0);
        IReadOnlyList<Card> hand = [Infantry1, Infantry2, Infantry3];
        var state = BuildTradeReadyState(actor, hand, troopsRemaining: 0, tradesCompleted: 0);
        var engine = new GameEngine(new QueuedDiceRoller());
        var notHeld = new TerritoryCard(new TerritoryId("Egypt"), CardSymbol.Artillery);

        var result = engine.Execute(state, new TradeCardsCommand(actor, [Infantry1, Infantry2, notHeld]));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidCardSet, rejection.Error.Code);
    }

    /// <summary>
    /// Builds a minimal 2-player state in the Reinforce phase where
    /// <paramref name="actor"/> holds exactly <paramref name="hand"/>.
    /// </summary>
    private static GameState BuildTradeReadyState(
        PlayerId actor, IReadOnlyList<Card> hand, int troopsRemaining, int tradesCompleted)
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
            new GameStatus.InProgress(),
            tradesCompleted);
    }
}
