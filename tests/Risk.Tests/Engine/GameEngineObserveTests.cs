using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

public class GameEngineObserveTests
{
    [Fact]
    public void Observe_reveals_the_viewers_own_hand()
    {
        var state = BuildTwoPlayerState();
        var engine = new GameEngine(new QueuedDiceRoller());

        var view = engine.Observe(state, new PlayerId(0));

        Assert.Equal(2, view.OwnHand.Count);
    }

    [Fact]
    public void Observe_redacts_other_players_hands_to_card_counts()
    {
        var state = BuildTwoPlayerState();
        var engine = new GameEngine(new QueuedDiceRoller());

        var view = engine.Observe(state, new PlayerId(0));

        Assert.Equal(3, view.OtherPlayersCardCounts[new PlayerId(1)]);
        Assert.False(view.OtherPlayersCardCounts.ContainsKey(new PlayerId(0)));
    }

    [Fact]
    public void Observe_is_symmetric_for_a_different_viewer()
    {
        var state = BuildTwoPlayerState();
        var engine = new GameEngine(new QueuedDiceRoller());

        var view = engine.Observe(state, new PlayerId(1));

        Assert.Equal(3, view.OwnHand.Count);
        Assert.Equal(2, view.OtherPlayersCardCounts[new PlayerId(0)]);
    }

    private static GameState BuildTwoPlayerState()
    {
        IReadOnlyList<Card> player0Hand = [new WildCard(), new WildCard()];
        IReadOnlyList<Card> player1Hand = [new WildCard(), new WildCard(), new WildCard()];

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(new PlayerId(0), player0Hand, false, 0),
            new PlayerState(new PlayerId(1), player1Hand, false, 0)
        ];

        return new GameState(
            new Dictionary<TerritoryId, TerritoryState>(),
            players,
            new TurnState(new PlayerId(0), TurnPhase.Reinforce),
            Deck.CreateStandard(),
            [],
            new GameStatus.InProgress());
    }
}
