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

public class VictoryTests
{
    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId NorthwestTerritory = new("NorthwestTerritory"); // adjacent to Alaska; defender's last territory

    [Fact]
    public void Execute_declares_victory_when_the_attacker_conquers_the_last_territory_not_already_theirs()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        var state = BuildOneTerritoryFromVictoryState(attacker, defender);
        var dice = new QueuedDiceRoller().Enqueue(6).Enqueue(1);
        var engine = new GameEngine(dice);

        var result = engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var won = Assert.IsType<GameStatus.Won>(ok.State.Status);
        Assert.Equal(attacker, won.Winner);
        var gameWon = Assert.Single(ok.Events.OfType<GameWon>());
        Assert.Equal(attacker, gameWon.Winner);
    }

    [Fact]
    public void Execute_rejects_any_command_once_the_game_has_already_been_won()
    {
        var winner = new PlayerId(0);
        var other = new PlayerId(1);
        var territories = WorldMap.Territories.ToDictionary(t => t.Id, t => new TerritoryState(winner, 1));

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(winner, [], false, 0),
            new PlayerState(other, [], true, 0)
        ];

        var state = new GameState(
            territories, players, new TurnState(winner, TurnPhase.Fortify), Deck.CreateStandard(), [], new GameStatus.Won(winner));
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new EndPhaseCommand(winner));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.GameOver, rejection.Error.Code);
    }

    /// <summary>
    /// Builds a state one conquest away from victory: <paramref name="attacker"/>
    /// owns every territory except <see cref="NorthwestTerritory"/> (the
    /// defender's sole remaining territory), so this attack both eliminates
    /// the defender and wins the game in the same command.
    /// </summary>
    private static GameState BuildOneTerritoryFromVictoryState(PlayerId attacker, PlayerId defender)
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>();
        foreach (var territory in WorldMap.Territories)
        {
            territories[territory.Id] = territory.Id == NorthwestTerritory
                ? new TerritoryState(defender, 1)
                : new TerritoryState(attacker, 1);
        }
        territories[Alaska] = new TerritoryState(attacker, 4);

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(attacker, [], false, 0),
            new PlayerState(defender, [], false, 0)
        ];

        return new GameState(
            territories, players, new TurnState(attacker, TurnPhase.Attack), Deck.CreateStandard(), [], new GameStatus.InProgress());
    }
}
