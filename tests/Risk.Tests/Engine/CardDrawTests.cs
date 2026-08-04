using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

/// <summary>
/// Covers the end-of-turn card draw: a player who conquered at least one
/// territory this turn draws one card from the deck when their turn ends,
/// unless the deck has already been exhausted.
/// </summary>
public class CardDrawTests
{
    [Fact]
    public void Execute_draws_a_card_and_raises_CardDrawn_when_the_departing_player_conquered_this_turn()
    {
        var actor = new PlayerId(0);
        var next = new PlayerId(1);
        var deck = Deck.CreateStandard();
        var state = BuildFortifyPhaseState(actor, next, conqueredThisTurn: true, deck: deck);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(actor));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var actorHand = ok.State.Players.Single(p => p.Id == actor).Hand;
        Assert.Single(actorHand);
        Assert.Equal(deck.Count - 1, ok.State.Deck.Count);
        var drawnEvent = Assert.Single(ok.Events.OfType<CardDrawn>());
        Assert.Equal(actor, drawnEvent.Actor);
        Assert.Equal(actorHand[0], drawnEvent.Card);
    }

    [Fact]
    public void Execute_does_not_draw_a_card_when_the_departing_player_did_not_conquer_this_turn()
    {
        var actor = new PlayerId(0);
        var next = new PlayerId(1);
        var deck = Deck.CreateStandard();
        var state = BuildFortifyPhaseState(actor, next, conqueredThisTurn: false, deck: deck);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(actor));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var actorHand = ok.State.Players.Single(p => p.Id == actor).Hand;
        Assert.Empty(actorHand);
        Assert.Equal(deck.Count, ok.State.Deck.Count);
        Assert.Empty(ok.Events.OfType<CardDrawn>());
    }

    [Fact]
    public void Execute_does_not_draw_a_card_when_the_deck_is_empty_even_if_the_departing_player_conquered()
    {
        var actor = new PlayerId(0);
        var next = new PlayerId(1);
        var state = BuildFortifyPhaseState(actor, next, conqueredThisTurn: true, deck: []);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(actor));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var actorHand = ok.State.Players.Single(p => p.Id == actor).Hand;
        Assert.Empty(actorHand);
        Assert.Empty(ok.State.Deck);
        Assert.Empty(ok.Events.OfType<CardDrawn>());
    }

    /// <summary>
    /// Builds a minimal 2-player state where <paramref name="actor"/> is in
    /// the Fortify phase and owns every territory except the 6 North
    /// American territories, which belong to <paramref name="next"/>.
    /// </summary>
    private static GameState BuildFortifyPhaseState(PlayerId actor, PlayerId next, bool conqueredThisTurn, IReadOnlyList<Card> deck)
    {
        TerritoryId[] nextTerritories =
        [
            new("Alaska"), new("NorthwestTerritory"), new("Greenland"),
            new("Alberta"), new("Ontario"), new("Quebec")
        ];

        var territories = new Dictionary<TerritoryId, TerritoryState>();
        foreach (var territory in WorldMap.Territories)
        {
            var owner = nextTerritories.Contains(territory.Id) ? next : actor;
            territories[territory.Id] = new TerritoryState(owner, 1);
        }

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(actor, [], false, 0),
            new PlayerState(next, [], false, 0)
        ];

        var turn = new TurnState(actor, TurnPhase.Fortify, ConqueredThisTurn: conqueredThisTurn, FortifyUsed: false);

        return new GameState(territories, players, turn, deck, [], new GameStatus.InProgress());
    }
}
