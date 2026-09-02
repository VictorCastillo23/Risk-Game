using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Modes;
using Risk.Engine.Results;
using Risk.Engine.Setup;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Modes;

/// <summary>
/// Characterization tests: these pin behavior that is already true through
/// the pre-refactor inline path and stays true after the
/// <c>Modes/</c> seam is wired — they are green before wiring and must
/// remain green after. They do not drive new behavior; they prove the new
/// seam introduced zero observable regression for SecretMission (2.6-2.7)
/// and for the three untouched modes (2.8).
/// </summary>
public class SecretMissionWiringTests
{
    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId NorthwestTerritory = new("NorthwestTerritory"); // adjacent to Alaska
    private static readonly TerritoryId Kamchatka = new("Kamchatka"); // far from Alaska/NorthwestTerritory; used as the neutral's holdout

    [Fact]
    public void GameSetup_Create_produces_a_valid_SecretMission_starting_state()
    {
        var result = GameSetup.Create(3, GameMode.SecretMission, QueuedDiceRoller.ForRollOff(3));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(GameMode.SecretMission, ok.State.Mode);
        Assert.Equal(WorldMap.Territories.Count, ok.State.Territories.Count);
        Assert.Equal(TurnPhase.Setup, ok.State.Turn.Phase);
        Assert.Equal(3, ok.State.Players.Count);
        var assigned = Assert.Single(ok.State.Log.OfType<TerritoriesAssigned>());
        Assert.Equal(WorldMap.Territories.Count, assigned.Assignments.Count);
        Assert.All(ok.State.Players, p => Assert.NotNull(p.Mission));
    }

    [Theory]
    [InlineData(GameMode.Classic)]
    [InlineData(GameMode.TwoPlayer)]
    [InlineData(GameMode.Capital)]
    public void GameSetup_Create_leaves_Mission_null_for_non_SecretMission_modes(GameMode mode)
    {
        var playerCount = mode == GameMode.TwoPlayer ? 2 : 3;
        var result = GameSetup.Create(playerCount, mode, QueuedDiceRoller.ForRollOff(playerCount));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.All(ok.State.Players, p => Assert.Null(p.Mission));
    }

    [Fact]
    public void SecretMission_conquest_of_the_last_territory_wins_through_IGameEngine_Execute()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        var state = BuildOneTerritoryFromVictoryState(attacker, defender, GameMode.SecretMission);
        var dice = new QueuedDiceRoller().Enqueue(6).Enqueue(1);
        var engine = new GameEngine(dice);

        var result = engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var won = Assert.IsType<GameStatus.Won>(ok.State.Status);
        Assert.Equal(attacker, won.Winner);
        var gameWon = Assert.Single(ok.Events.OfType<GameWon>());
        Assert.Equal(attacker, gameWon.Winner);
    }

    [Theory]
    [InlineData(GameMode.Capital)]
    public void Other_modes_still_win_via_the_untouched_actor_only_inline_check(GameMode mode)
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        var state = BuildOneTerritoryFromVictoryState(attacker, defender, mode);
        var dice = new QueuedDiceRoller().Enqueue(6).Enqueue(1);
        var engine = new GameEngine(dice);

        var result = engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var won = Assert.IsType<GameStatus.Won>(ok.State.Status);
        Assert.Equal(attacker, won.Winner);
        var gameWon = Assert.Single(ok.Events.OfType<GameWon>());
        Assert.Equal(attacker, gameWon.Winner);
    }

    /// <summary>
    /// Positive routing proof (hard requirement, design D5): a Classic win
    /// alone is NOT sufficient evidence that <see cref="ConquestVictoryRule"/>
    /// is actually wired, because its logic is byte-identical to the inline
    /// fallback it replaces — a Classic win test could pass by coincidence
    /// even if <c>ExecuteAttack</c> never called the rule at all. Injecting a
    /// <see cref="RecordingVictoryRule"/> via the internal test constructor
    /// gives positive, observable proof the interface path is live.
    /// </summary>
    [Fact]
    public void Classic_victory_is_routed_through_ConquestVictoryRule()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        var state = BuildOneTerritoryFromVictoryState(attacker, defender, GameMode.Classic);
        var dice = new QueuedDiceRoller().Enqueue(6).Enqueue(1);
        var recordingRule = new RecordingVictoryRule(new ConquestVictoryRule());
        Func<GameMode, IVictoryRule?> victoryRuleFor = mode =>
            mode == GameMode.Classic ? recordingRule : VictoryRules.For(mode);
        var engine = new GameEngine(dice, victoryRuleFor);

        var result = engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var won = Assert.IsType<GameStatus.Won>(ok.State.Status);
        Assert.Equal(attacker, won.Winner);
        Assert.Equal(1, recordingRule.Calls);
    }

    /// <summary>
    /// Positive routing proof for <see cref="GameMode.TwoPlayer"/> (item 4.3),
    /// mirroring <see cref="Classic_victory_is_routed_through_ConquestVictoryRule"/>:
    /// a TwoPlayer win alone is not sufficient evidence that
    /// <see cref="TwoPlayerVictoryRule"/> is actually wired, because a
    /// same-shaped 2-humans-only state (no neutral) would also satisfy
    /// <see cref="ConquestVictoryRule"/>'s "owns everything" check by
    /// coincidence. Building the state with a neutral player that still owns
    /// territory when the second human is eliminated, and asserting the
    /// <see cref="RecordingVictoryRule"/>'s call count, gives positive,
    /// observable proof that <c>ExecuteAttack</c> reaches
    /// <see cref="TwoPlayerVictoryRule"/> via branch 2 — not the branch-3
    /// inline fallback, and not <see cref="ConquestVictoryRule"/>.
    /// </summary>
    [Fact]
    public void TwoPlayer_victory_is_routed_through_TwoPlayerVictoryRule()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        var neutral = new PlayerId(2);
        var state = BuildTwoPlayerOneTerritoryFromVictoryState(attacker, defender, neutral);
        var dice = new QueuedDiceRoller().Enqueue(6).Enqueue(1);
        var recordingRule = new RecordingVictoryRule(new TwoPlayerVictoryRule());
        Func<GameMode, IVictoryRule?> victoryRuleFor = mode =>
            mode == GameMode.TwoPlayer ? recordingRule : VictoryRules.For(mode);
        var engine = new GameEngine(dice, victoryRuleFor);

        var result = engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var won = Assert.IsType<GameStatus.Won>(ok.State.Status);
        Assert.Equal(attacker, won.Winner);
        Assert.Equal(1, recordingRule.Calls);
    }

    /// <summary>
    /// Builds a state one conquest away from victory: <paramref name="attacker"/>
    /// owns every territory except <see cref="NorthwestTerritory"/> (the
    /// defender's sole remaining territory), so this attack both eliminates
    /// the defender and wins the game in the same command. Mirrors
    /// <c>VictoryTests.BuildOneTerritoryFromVictoryState</c>, parameterized
    /// by <see cref="GameMode"/>.
    /// </summary>
    private static GameState BuildOneTerritoryFromVictoryState(PlayerId attacker, PlayerId defender, GameMode mode)
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
            territories, players, new TurnState(attacker, TurnPhase.Attack), Deck.CreateStandard(), [], new GameStatus.InProgress(), Mode: mode);
    }

    /// <summary>
    /// TwoPlayer-shaped variant of <see cref="BuildOneTerritoryFromVictoryState"/>
    /// (design D4): 2 humans + 1 <see cref="PlayerState.IsNeutral"/> player.
    /// <paramref name="attacker"/> owns every territory except
    /// <see cref="NorthwestTerritory"/> (the defender's sole remaining
    /// territory) and <see cref="Kamchatka"/> (owned by the neutral, who
    /// keeps it through and after this attack) — so this conquest both
    /// eliminates the defender and wins the game, while the neutral still
    /// holds territory, proving <see cref="TwoPlayerVictoryRule"/>'s
    /// elimination-based check rather than a territory-count check.
    /// </summary>
    private static GameState BuildTwoPlayerOneTerritoryFromVictoryState(PlayerId attacker, PlayerId defender, PlayerId neutral)
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>();
        foreach (var territory in WorldMap.Territories)
        {
            territories[territory.Id] = territory.Id switch
            {
                _ when territory.Id == NorthwestTerritory => new TerritoryState(defender, 1),
                _ when territory.Id == Kamchatka => new TerritoryState(neutral, 1),
                _ => new TerritoryState(attacker, 1)
            };
        }
        territories[Alaska] = new TerritoryState(attacker, 4);

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(attacker, [], false, 0),
            new PlayerState(defender, [], false, 0),
            new PlayerState(neutral, [], false, 0, IsNeutral: true)
        ];

        return new GameState(
            territories, players, new TurnState(attacker, TurnPhase.Attack), Deck.CreateStandard(), [], new GameStatus.InProgress(), Mode: GameMode.TwoPlayer);
    }
}
