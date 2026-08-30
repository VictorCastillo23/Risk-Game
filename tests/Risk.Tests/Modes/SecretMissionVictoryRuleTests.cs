using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Modes;
using Risk.Engine.State;

namespace Risk.Tests.Modes;

public class SecretMissionVictoryRuleTests
{
    private static readonly IVictoryRule Rule = new SecretMissionVictoryRule();

    [Fact]
    public void CheckVictory_returns_the_owner_when_one_player_owns_all_42_territories()
    {
        var owner = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildState(
            territoryOwners: WorldMap.Territories.Select(_ => owner).ToArray(),
            players:
            [
                new PlayerState(owner, [], false, 0),
                new PlayerState(other, [], false, 0)
            ]);

        var winner = Rule.CheckVictory(state);

        Assert.Equal(owner, winner);
    }

    [Fact]
    public void CheckVictory_returns_null_when_nobody_owns_all_42_territories()
    {
        var playerA = new PlayerId(0);
        var playerB = new PlayerId(1);
        var owners = WorldMap.Territories
            .Select((t, i) => i == 0 ? playerB : playerA)
            .ToArray();
        var state = BuildState(
            territoryOwners: owners,
            players:
            [
                new PlayerState(playerA, [], false, 0),
                new PlayerState(playerB, [], false, 0)
            ]);

        var winner = Rule.CheckVictory(state);

        Assert.Null(winner);
    }

    [Fact]
    public void CheckVictory_reports_a_non_actor_player_who_owns_all_42_territories()
    {
        var playerA = new PlayerId(0); // Turn.CurrentPlayer (the "actor")
        var playerB = new PlayerId(1); // owns everything, but is not the current player
        var state = BuildState(
            territoryOwners: WorldMap.Territories.Select(_ => playerB).ToArray(),
            players:
            [
                new PlayerState(playerA, [], false, 0),
                new PlayerState(playerB, [], false, 0)
            ],
            currentPlayer: playerA);

        var winner = Rule.CheckVictory(state);

        Assert.Equal(playerB, winner);
    }

    [Fact]
    public void CheckVictory_never_reports_an_eliminated_player_even_if_the_territory_data_shows_them_owning_everything()
    {
        var eliminated = new PlayerId(0);
        var active = new PlayerId(1);
        var state = BuildState(
            territoryOwners: WorldMap.Territories.Select(_ => eliminated).ToArray(),
            players:
            [
                new PlayerState(eliminated, [], true, 0),
                new PlayerState(active, [], false, 0)
            ]);

        var winner = Rule.CheckVictory(state);

        Assert.Null(winner);
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
            territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress(), Mode: GameMode.SecretMission);
    }
}
