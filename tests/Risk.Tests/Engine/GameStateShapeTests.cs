using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Tests.Engine;

public class GameStateShapeTests
{
    [Fact]
    public void TerritoryState_holds_owner_and_troop_count()
    {
        var territory = new TerritoryState(new PlayerId(1), 4);

        Assert.Equal(new PlayerId(1), territory.Owner);
        Assert.Equal(4, territory.Troops);
    }

    [Fact]
    public void PlayerState_holds_hand_elimination_and_remaining_troops()
    {
        IReadOnlyList<Card> hand = [new WildCard()];
        var player = new PlayerState(new PlayerId(0), hand, false, 5);

        Assert.Equal(new PlayerId(0), player.Id);
        Assert.Single(player.Hand);
        Assert.False(player.IsEliminated);
        Assert.Equal(5, player.TroopsRemaining);
    }

    [Fact]
    public void TurnState_holds_current_player_and_phase()
    {
        var turn = new TurnState(new PlayerId(2), TurnPhase.Reinforce);

        Assert.Equal(new PlayerId(2), turn.CurrentPlayer);
        Assert.Equal(TurnPhase.Reinforce, turn.Phase);
    }

    [Fact]
    public void GameStatus_InProgress_and_Won_are_distinct()
    {
        GameStatus inProgress = new GameStatus.InProgress();
        GameStatus won = new GameStatus.Won(new PlayerId(3));

        Assert.IsType<GameStatus.InProgress>(inProgress);
        var winner = Assert.IsType<GameStatus.Won>(won);
        Assert.Equal(new PlayerId(3), winner.Winner);
    }

    [Fact]
    public void GameState_composes_territories_players_turn_deck_log_and_status()
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>
        {
            [new TerritoryId("Alaska")] = new TerritoryState(new PlayerId(0), 3)
        };
        IReadOnlyList<PlayerState> players = [new PlayerState(new PlayerId(0), [], false, 0)];
        var turn = new TurnState(new PlayerId(0), TurnPhase.Reinforce);
        var deck = Deck.CreateStandard();
        IReadOnlyList<Risk.Engine.Events.GameEvent> log = [];

        var state = new GameState(territories, players, turn, deck, log, new GameStatus.InProgress());

        Assert.Same(territories, state.Territories);
        Assert.Equal(44, state.Deck.Count);
        Assert.Equal(TurnPhase.Reinforce, state.Turn.Phase);
        Assert.Empty(state.Log);
        Assert.IsType<GameStatus.InProgress>(state.Status);
    }
}
