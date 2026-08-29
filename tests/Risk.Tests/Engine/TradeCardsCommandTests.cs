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
    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId Alberta = new("Alberta");
    private static readonly TerritoryId Ontario = new("Ontario");
    private static readonly TerritoryId Brazil = new("Brazil");

    private static readonly TerritoryCard Infantry1 = new(Alaska, CardSymbol.Infantry);
    private static readonly TerritoryCard Infantry2 = new(Alberta, CardSymbol.Infantry);
    private static readonly TerritoryCard Infantry3 = new(Ontario, CardSymbol.Infantry);
    private static readonly TerritoryCard Cavalry1 = new(Brazil, CardSymbol.Cavalry);

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
        Assert.Null(traded.BonusTerritory); // 0 owned-territory matches: no bonus
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

    [Fact]
    public void Execute_rejects_a_bonus_territory_when_there_are_no_owned_matches()
    {
        var actor = new PlayerId(0);
        IReadOnlyList<Card> hand = [Infantry1, Infantry2, Infantry3];
        var state = BuildTradeReadyState(actor, hand, troopsRemaining: 0, tradesCompleted: 0);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new TradeCardsCommand(actor, hand, Alaska));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidBonusTerritory, rejection.Error.Code);
    }

    [Fact]
    public void Execute_automatically_applies_the_bonus_when_exactly_one_owned_match_and_null_is_supplied()
    {
        var actor = new PlayerId(0);
        IReadOnlyList<Card> hand = [Infantry1, Infantry2, Infantry3];
        var state = BuildTradeReadyState(actor, hand, troopsRemaining: 0, tradesCompleted: 0, actorOwnedTerritories: [Alaska]);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new TradeCardsCommand(actor, hand));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(3, ok.State.Territories[Alaska].Troops); // 1 starting + 2 bonus
        var player = ok.State.Players.Single(p => p.Id == actor);
        Assert.Equal(4, player.TroopsRemaining); // pool bonus only, +2 never enters the pool
        var traded = Assert.Single(ok.Events.OfType<CardsTraded>());
        Assert.Equal(Alaska, traded.BonusTerritory);
    }

    [Fact]
    public void Execute_rejects_a_wrong_explicit_bonus_territory_when_exactly_one_owned_match()
    {
        var actor = new PlayerId(0);
        IReadOnlyList<Card> hand = [Infantry1, Infantry2, Infantry3];
        var state = BuildTradeReadyState(actor, hand, troopsRemaining: 0, tradesCompleted: 0, actorOwnedTerritories: [Alaska, Brazil]);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new TradeCardsCommand(actor, hand, Brazil));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidBonusTerritory, rejection.Error.Code);
        Assert.Equal(1, state.Territories[Alaska].Troops); // nothing moved
    }

    [Fact]
    public void Execute_rejects_when_multiple_owned_matches_and_null_is_supplied()
    {
        var actor = new PlayerId(0);
        IReadOnlyList<Card> hand = [Infantry1, Infantry2, Infantry3];
        var state = BuildTradeReadyState(actor, hand, troopsRemaining: 0, tradesCompleted: 0, actorOwnedTerritories: [Alaska, Alberta]);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new TradeCardsCommand(actor, hand));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidBonusTerritory, rejection.Error.Code);
    }

    [Fact]
    public void Execute_applies_the_bonus_only_to_the_chosen_territory_when_multiple_owned_matches()
    {
        var actor = new PlayerId(0);
        IReadOnlyList<Card> hand = [Infantry1, Infantry2, Infantry3];
        var state = BuildTradeReadyState(actor, hand, troopsRemaining: 0, tradesCompleted: 0, actorOwnedTerritories: [Alaska, Alberta]);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new TradeCardsCommand(actor, hand, Alberta));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(3, ok.State.Territories[Alberta].Troops); // chosen territory gets +2
        Assert.Equal(1, ok.State.Territories[Alaska].Troops); // the other match is unaffected
        var traded = Assert.Single(ok.Events.OfType<CardsTraded>());
        Assert.Equal(Alberta, traded.BonusTerritory);
    }

    [Fact]
    public void Execute_rejects_a_bonus_territory_outside_the_matches_when_multiple_owned_matches()
    {
        var actor = new PlayerId(0);
        IReadOnlyList<Card> hand = [Infantry1, Infantry2, Infantry3];
        var state = BuildTradeReadyState(
            actor, hand, troopsRemaining: 0, tradesCompleted: 0,
            actorOwnedTerritories: [Alaska, Alberta, Brazil]); // Brazil is owned but not a traded-card match

        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new TradeCardsCommand(actor, hand, Brazil));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidBonusTerritory, rejection.Error.Code);
    }

    [Fact]
    public void Execute_auto_applies_the_single_match_when_the_traded_set_includes_a_wildcard()
    {
        var actor = new PlayerId(0);
        IReadOnlyList<Card> hand = [Infantry1, Infantry3, new WildCard()]; // 2 Infantry + wildcard: valid set
        var state = BuildTradeReadyState(actor, hand, troopsRemaining: 0, tradesCompleted: 0, actorOwnedTerritories: [Alaska]);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new TradeCardsCommand(actor, hand));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(3, ok.State.Territories[Alaska].Troops); // wildcard never contributes a second match
        var traded = Assert.Single(ok.Events.OfType<CardsTraded>());
        Assert.Equal(Alaska, traded.BonusTerritory);
    }

    /// <summary>
    /// Builds a minimal 2-player state in the Reinforce phase where
    /// <paramref name="actor"/> holds exactly <paramref name="hand"/> and
    /// owns only <paramref name="actorOwnedTerritories"/> (default: none) —
    /// every other territory on the board belongs to the other player, so
    /// the occupied-territory bonus's ownership matching is exercised for
    /// real rather than trivially satisfied.
    /// </summary>
    private static GameState BuildTradeReadyState(
        PlayerId actor,
        IReadOnlyList<Card> hand,
        int troopsRemaining,
        int tradesCompleted,
        IReadOnlyList<TerritoryId>? actorOwnedTerritories = null)
    {
        var other = new PlayerId(actor.Value + 1);
        var owned = new HashSet<TerritoryId>(actorOwnedTerritories ?? []);
        var territories = WorldMap.Territories.ToDictionary(
            t => t.Id,
            t => new TerritoryState(owned.Contains(t.Id) ? actor : other, 1));

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
