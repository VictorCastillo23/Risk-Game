using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Modes;
using Risk.Engine.State;

namespace Risk.Tests.Modes;

public class CapitalVictoryRuleTests
{
    private static readonly IVictoryRule Rule = new CapitalVictoryRule();

    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId Greenland = new("Greenland");
    private static readonly TerritoryId Ontario = new("Ontario");
    private static readonly TerritoryId Alberta = new("Alberta");
    private static readonly TerritoryId NorthwestTerritory = new("NorthwestTerritory");

    [Fact]
    public void CheckVictory_returns_winner_in_3_player_game()
    {
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var c = new PlayerId(2);
        var players = new[]
        {
            new PlayerState(a, [], false, 0, HeadquartersId: Alaska),
            new PlayerState(b, [], false, 0, HeadquartersId: Greenland),
            new PlayerState(c, [], false, 0, HeadquartersId: Ontario)
        };
        var state = BuildState(
            players,
            territoryOverrides: new Dictionary<TerritoryId, PlayerId> { [Alaska] = a, [Greenland] = a, [Ontario] = a },
            defaultOwner: a);

        var winner = Rule.CheckVictory(state);

        Assert.Equal(a, winner);
    }

    [Fact]
    public void CheckVictory_returns_winner_in_4_player_game()
    {
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var c = new PlayerId(2);
        var d = new PlayerId(3);
        var players = new[]
        {
            new PlayerState(a, [], false, 0, HeadquartersId: Alaska),
            new PlayerState(b, [], false, 0, HeadquartersId: Greenland),
            new PlayerState(c, [], false, 0, HeadquartersId: Ontario),
            new PlayerState(d, [], false, 0, HeadquartersId: Alberta)
        };
        var state = BuildState(
            players,
            territoryOverrides: new Dictionary<TerritoryId, PlayerId>
            {
                [Alaska] = a,
                [Greenland] = a,
                [Ontario] = a,
                [Alberta] = a
            },
            defaultOwner: a);

        var winner = Rule.CheckVictory(state);

        Assert.Equal(a, winner);
    }

    [Fact]
    public void CheckVictory_returns_winner_in_5_player_game()
    {
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var c = new PlayerId(2);
        var d = new PlayerId(3);
        var e = new PlayerId(4);
        var players = new[]
        {
            new PlayerState(a, [], false, 0, HeadquartersId: Alaska),
            new PlayerState(b, [], false, 0, HeadquartersId: Greenland),
            new PlayerState(c, [], false, 0, HeadquartersId: Ontario),
            new PlayerState(d, [], false, 0, HeadquartersId: Alberta),
            new PlayerState(e, [], false, 0, HeadquartersId: NorthwestTerritory)
        };
        var state = BuildState(
            players,
            territoryOverrides: new Dictionary<TerritoryId, PlayerId>
            {
                [Alaska] = a,
                [Greenland] = a,
                [Ontario] = a,
                [Alberta] = a,
                [NorthwestTerritory] = a
            },
            defaultOwner: a);

        var winner = Rule.CheckVictory(state);

        Assert.Equal(a, winner);
    }

    [Fact]
    public void CheckVictory_returns_null_when_own_HQ_lost_despite_owning_all_opponent_HQs()
    {
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var c = new PlayerId(2);
        var players = new[]
        {
            new PlayerState(a, [], false, 0, HeadquartersId: Alaska),
            new PlayerState(b, [], false, 0, HeadquartersId: Greenland),
            new PlayerState(c, [], false, 0, HeadquartersId: Ontario)
        };
        // B holds A's own HQ (Alaska); A still owns both opponent HQs.
        var state = BuildState(
            players,
            territoryOverrides: new Dictionary<TerritoryId, PlayerId> { [Alaska] = b, [Greenland] = a, [Ontario] = a },
            defaultOwner: a);

        var winner = Rule.CheckVictory(state);

        Assert.Null(winner);
    }

    [Fact]
    public void CheckVictory_returns_winner_when_own_HQ_recaptured_after_owning_all_opponent_HQs()
    {
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var c = new PlayerId(2);
        var players = new[]
        {
            new PlayerState(a, [], false, 0, HeadquartersId: Alaska),
            new PlayerState(b, [], false, 0, HeadquartersId: Greenland),
            new PlayerState(c, [], false, 0, HeadquartersId: Ontario)
        };
        var lostState = BuildState(
            players,
            territoryOverrides: new Dictionary<TerritoryId, PlayerId> { [Alaska] = b, [Greenland] = a, [Ontario] = a },
            defaultOwner: a);
        Assert.Null(Rule.CheckVictory(lostState));

        var recapturedState = BuildState(
            players,
            territoryOverrides: new Dictionary<TerritoryId, PlayerId> { [Alaska] = a, [Greenland] = a, [Ontario] = a },
            defaultOwner: a);

        var winner = Rule.CheckVictory(recapturedState);

        Assert.Equal(a, winner);
    }

    [Fact]
    public void CheckVictory_returns_null_when_one_opponent_HQ_still_uncaptured()
    {
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var c = new PlayerId(2);
        var d = new PlayerId(3);
        var players = new[]
        {
            new PlayerState(a, [], false, 0, HeadquartersId: Alaska),
            new PlayerState(b, [], false, 0, HeadquartersId: Greenland),
            new PlayerState(c, [], false, 0, HeadquartersId: Ontario),
            new PlayerState(d, [], false, 0, HeadquartersId: Alberta)
        };
        // A owns its own HQ plus B's and C's, but D still holds its own HQ (Alberta).
        var state = BuildState(
            players,
            territoryOverrides: new Dictionary<TerritoryId, PlayerId>
            {
                [Alaska] = a,
                [Greenland] = a,
                [Ontario] = a,
                [Alberta] = d
            },
            defaultOwner: a);

        var winner = Rule.CheckVictory(state);

        Assert.Null(winner);
    }

    [Fact]
    public void CheckVictory_counts_HQ_owned_via_third_party_elimination()
    {
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var c = new PlayerId(2); // eliminated by B, not by A
        var players = new[]
        {
            new PlayerState(a, [], false, 0, HeadquartersId: Alaska),
            new PlayerState(b, [], false, 0, HeadquartersId: Greenland),
            new PlayerState(c, [], true, 0, HeadquartersId: Ontario)
        };
        // A ends up owning C's former HQ territory (captured from B, who eliminated C).
        var state = BuildState(
            players,
            territoryOverrides: new Dictionary<TerritoryId, PlayerId> { [Alaska] = a, [Greenland] = a, [Ontario] = a },
            defaultOwner: a);

        var winner = Rule.CheckVictory(state);

        Assert.Equal(a, winner);
    }

    [Fact]
    public void CheckVictory_returns_null_when_HeadquartersId_is_null()
    {
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var players = new[]
        {
            // A's own HeadquartersId is unset — must be skipped, not crash, even
            // though A owns every territory (including B's HQ).
            new PlayerState(a, [], false, 0, HeadquartersId: null),
            new PlayerState(b, [], false, 0, HeadquartersId: Greenland)
        };
        var state = BuildState(
            players,
            territoryOverrides: new Dictionary<TerritoryId, PlayerId> { [Greenland] = b },
            defaultOwner: a);

        var winner = Rule.CheckVictory(state);

        // Neither player wins: A is skipped (no own HQ to check), and B cannot
        // satisfy "owns every opponent HQ" because A's HeadquartersId is null.
        Assert.Null(winner);
    }

    [Fact]
    public void VictoryRules_For_Capital_resolves_to_CapitalVictoryRule()
    {
        var rule = VictoryRules.For(GameMode.Capital);

        Assert.IsType<CapitalVictoryRule>(rule);
    }

    private static GameState BuildState(
        IReadOnlyList<PlayerState> players,
        IReadOnlyDictionary<TerritoryId, PlayerId> territoryOverrides,
        PlayerId defaultOwner,
        PlayerId? currentPlayer = null)
    {
        var territories = WorldMap.Territories.ToDictionary(
            t => t.Id,
            t => new TerritoryState(territoryOverrides.TryGetValue(t.Id, out var owner) ? owner : defaultOwner, 1));

        var turn = new TurnState(currentPlayer ?? players[0].Id, TurnPhase.Attack);

        return new GameState(
            territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress(), Mode: GameMode.Capital);
    }
}
