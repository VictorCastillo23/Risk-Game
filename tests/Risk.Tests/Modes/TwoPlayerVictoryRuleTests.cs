using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Modes;
using Risk.Engine.State;

namespace Risk.Tests.Modes;

public class TwoPlayerVictoryRuleTests
{
    private static readonly IVictoryRule Rule = new TwoPlayerVictoryRule();

    [Fact]
    public void CheckVictory_returns_the_sole_surviving_human_when_the_other_human_is_eliminated()
    {
        var survivor = new PlayerId(0);
        var eliminated = new PlayerId(1);
        var neutral = new PlayerId(2);
        var state = BuildState(
            territoryOwners: WorldMap.Territories.Select(_ => survivor).ToArray(),
            players:
            [
                new PlayerState(survivor, [], false, 0),
                new PlayerState(eliminated, [], true, 0),
                new PlayerState(neutral, [], false, 0, IsNeutral: true)
            ]);

        var winner = Rule.CheckVictory(state);

        Assert.Equal(survivor, winner);
    }

    [Fact]
    public void CheckVictory_returns_null_while_both_humans_hold_territory_regardless_of_the_neutrals_territory_count()
    {
        var playerA = new PlayerId(0);
        var playerB = new PlayerId(1);
        var neutral = new PlayerId(2);
        var owners = WorldMap.Territories
            .Select((_, i) => i % 3 == 0 ? playerA : i % 3 == 1 ? playerB : neutral)
            .ToArray();
        var state = BuildState(
            territoryOwners: owners,
            players:
            [
                new PlayerState(playerA, [], false, 0),
                new PlayerState(playerB, [], false, 0),
                new PlayerState(neutral, [], false, 0, IsNeutral: true)
            ]);

        var winner = Rule.CheckVictory(state);

        Assert.Null(winner);
    }

    [Fact]
    public void CheckVictory_never_reports_the_neutral_as_winner_even_when_it_owns_most_territories()
    {
        var playerA = new PlayerId(0);
        var playerB = new PlayerId(1);
        var neutral = new PlayerId(2);

        var bothAliveOwners = WorldMap.Territories
            .Select((_, i) => i == 0 ? playerA : i == 1 ? playerB : neutral)
            .ToArray();
        var bothAliveState = BuildState(
            territoryOwners: bothAliveOwners,
            players:
            [
                new PlayerState(playerA, [], false, 0),
                new PlayerState(playerB, [], false, 0),
                new PlayerState(neutral, [], false, 0, IsNeutral: true)
            ]);

        Assert.Null(Rule.CheckVictory(bothAliveState));

        var oneEliminatedState = BuildState(
            territoryOwners: bothAliveOwners,
            players:
            [
                new PlayerState(playerA, [], false, 0),
                new PlayerState(playerB, [], true, 0),
                new PlayerState(neutral, [], false, 0, IsNeutral: true)
            ]);

        var winner = Rule.CheckVictory(oneEliminatedState);

        Assert.Equal(playerA, winner);
    }

    [Fact]
    public void CheckVictory_declares_the_survivor_even_though_the_neutral_still_owns_most_of_the_board()
    {
        var survivor = new PlayerId(0);
        var eliminated = new PlayerId(1);
        var neutral = new PlayerId(2);
        // Survivor owns far fewer than all 42 territories — the anti-ConquestVictoryRule assertion:
        // territory count must be irrelevant to TwoPlayerVictoryRule.
        var owners = WorldMap.Territories
            .Select((_, i) => i == 0 ? survivor : neutral)
            .ToArray();
        var state = BuildState(
            territoryOwners: owners,
            players:
            [
                new PlayerState(survivor, [], false, 0),
                new PlayerState(eliminated, [], true, 0),
                new PlayerState(neutral, [], false, 0, IsNeutral: true)
            ]);

        var winner = Rule.CheckVictory(state);

        Assert.Equal(survivor, winner);
    }

    [Fact]
    public void CheckVictory_returns_null_and_does_not_throw_in_the_unreachable_both_humans_eliminated_case()
    {
        var playerA = new PlayerId(0);
        var playerB = new PlayerId(1);
        var neutral = new PlayerId(2);
        var state = BuildState(
            territoryOwners: WorldMap.Territories.Select(_ => neutral).ToArray(),
            players:
            [
                new PlayerState(playerA, [], true, 0),
                new PlayerState(playerB, [], true, 0),
                new PlayerState(neutral, [], false, 0, IsNeutral: true)
            ]);

        var winner = Rule.CheckVictory(state);

        Assert.Null(winner);
    }

    [Fact]
    public void VictoryRules_For_TwoPlayer_resolves_to_TwoPlayerVictoryRule()
    {
        var rule = VictoryRules.For(GameMode.TwoPlayer);

        Assert.IsType<TwoPlayerVictoryRule>(rule);
    }

    [Fact]
    public void VictoryRules_For_Capital_still_resolves_to_null()
    {
        var rule = VictoryRules.For(GameMode.Capital);

        Assert.Null(rule);
    }

    private static GameState BuildState(
        IReadOnlyList<PlayerId> territoryOwners,
        IReadOnlyList<PlayerState> players,
        PlayerId? currentPlayer = null)
    {
        var territories = WorldMap.Territories
            .Select((t, i) => (t.Id, Owner: territoryOwners[i]))
            .ToDictionary(x => x.Id, x => new TerritoryState(x.Owner, 1));

        var turn = new TurnState(currentPlayer ?? players[0].Id, TurnPhase.Attack);

        return new GameState(
            territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress(), Mode: GameMode.TwoPlayer);
    }
}
