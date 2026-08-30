using Risk.Domain.Players;
using Risk.Engine.Setup;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

public class TurnOrderTests
{
    [Fact]
    public void DetermineFirst_returns_the_unique_highest_roller()
    {
        var players = new[] { new PlayerId(0), new PlayerId(1), new PlayerId(2) };
        var dice = new QueuedDiceRoller()
            .Enqueue(2)
            .Enqueue(6)
            .Enqueue(4);

        var winner = TurnOrder.DetermineFirst(players, dice);

        Assert.Equal(new PlayerId(1), winner);
    }

    [Fact]
    public void DetermineFirst_re_rolls_only_the_tied_players_and_stops_rolling_once_resolved()
    {
        var players = new[] { new PlayerId(0), new PlayerId(1), new PlayerId(2) };
        var dice = new QueuedDiceRoller()
            .Enqueue(6)
            .Enqueue(6)
            .Enqueue(2)
            .Enqueue(3)
            .Enqueue(5);

        var winner = TurnOrder.DetermineFirst(players, dice);

        Assert.Equal(new PlayerId(1), winner);
        Assert.Throws<InvalidOperationException>(() => dice.Roll(1));
    }

    [Fact]
    public void DetermineFirst_resolves_a_three_way_tie()
    {
        var players = new[] { new PlayerId(0), new PlayerId(1), new PlayerId(2), new PlayerId(3) };
        var dice = new QueuedDiceRoller()
            .Enqueue(5)
            .Enqueue(5)
            .Enqueue(5)
            .Enqueue(1)
            .Enqueue(4)
            .Enqueue(6)
            .Enqueue(2);

        var winner = TurnOrder.DetermineFirst(players, dice);

        Assert.Equal(new PlayerId(1), winner);
    }

    [Fact]
    public void DetermineFirst_resolves_a_tie_across_multiple_rounds()
    {
        var players = new[] { new PlayerId(0), new PlayerId(1), new PlayerId(2) };
        var dice = new QueuedDiceRoller()
            .Enqueue(6)
            .Enqueue(6)
            .Enqueue(1)
            .Enqueue(4)
            .Enqueue(4)
            .Enqueue(2)
            .Enqueue(5);

        var winner = TurnOrder.DetermineFirst(players, dice);

        Assert.Equal(new PlayerId(1), winner);
    }

    [Fact]
    public void DetermineFirst_returns_the_only_player_in_a_single_player_list()
    {
        var players = new[] { new PlayerId(0) };
        var dice = new QueuedDiceRoller()
            .Enqueue(3);

        var winner = TurnOrder.DetermineFirst(players, dice);

        Assert.Equal(new PlayerId(0), winner);
    }

    [Fact]
    public void DetermineFirst_throws_ArgumentException_for_an_empty_list()
    {
        var players = Array.Empty<PlayerId>();
        var dice = new QueuedDiceRoller();

        Assert.Throws<ArgumentException>(() => TurnOrder.DetermineFirst(players, dice));
    }
}
