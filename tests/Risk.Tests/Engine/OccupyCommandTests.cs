using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Modes;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

/// <summary>
/// Design 3.3-D7: a second <see cref="IVictoryRule.CheckVictory"/> call site
/// at the end of <c>GameEngine.ExecuteOccupy</c>, added because troop-gated
/// missions (every <see cref="OccupyTerritories"/> card) cannot be satisfied
/// by <c>ExecuteAttack</c>'s call site alone — the conquered territory still
/// holds 0 troops there. Without the second call site, a board-clearing final
/// conquest that also completes an <c>OccupyTerritories</c> mission leaves
/// <see cref="GameStatus"/> pinned at <see cref="GameStatus.InProgress"/>
/// forever, since no legal <see cref="AttackCommand"/> remains once one
/// player owns every territory.
/// </summary>
public class OccupyCommandTests
{
    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId NorthwestTerritory = new("NorthwestTerritory"); // adjacent to Alaska; used as the conquered territory
    private static readonly TerritoryId Kamchatka = new("Kamchatka"); // far from Alaska; used as the filler owner's holdout

    /// <summary>
    /// The stuck-game regression itself: the same final conquest that would
    /// clear the board also completes the actor's <c>OccupyTerritories</c>
    /// mission, but only once <see cref="OccupyCommand"/> sets the conquered
    /// territory's troop count. Driven through the real
    /// <see cref="AttackCommand"/> → <see cref="OccupyCommand"/> flow (not a
    /// hand-built <c>PendingOccupation</c>) so the test proves both call
    /// sites' ordering, not just the second one in isolation.
    /// </summary>
    [Fact]
    public void A_board_clearing_final_conquest_completes_OccupyTerritories_only_once_ExecuteOccupy_sets_the_troop_count()
    {
        var attacker = new PlayerId(0);
        var defender = new PlayerId(1);
        var mission = new OccupyTerritories(Count: WorldMap.Territories.Count, MinArmiesPerTerritory: 1);
        var state = BuildBoardClearingAttackState(attacker, defender, mission);
        var dice = new QueuedDiceRoller().Enqueue(6).Enqueue(1);
        var engine = new GameEngine(dice);

        var attackResult = engine.Execute(state, new AttackCommand(attacker, Alaska, NorthwestTerritory, 1));

        var attackOk = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(attackResult);
        Assert.IsType<GameStatus.InProgress>(attackOk.State.Status);
        Assert.DoesNotContain(attackOk.Events, e => e is GameWon);

        var occupyResult = engine.Execute(attackOk.State, new OccupyCommand(attacker, 1));

        var occupyOk = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(occupyResult);
        var won = Assert.IsType<GameStatus.Won>(occupyOk.State.Status);
        Assert.Equal(attacker, won.Winner);
        Assert.Contains(occupyOk.Events, e => e is GameWon);
    }

    /// <summary>
    /// Occupying with fewer troops than the mission's own
    /// <see cref="OccupyTerritories.MinArmiesPerTerritory"/> threshold (even
    /// though it satisfies the engine's own <c>PendingOccupation.MinimumTroops</c>
    /// floor) must not falsely complete the mission.
    /// </summary>
    [Fact]
    public void ExecuteOccupy_stays_InProgress_when_the_occupied_troop_count_is_below_the_missions_own_threshold()
    {
        var mission = new OccupyTerritories(Count: 41, MinArmiesPerTerritory: 2);
        var state = BuildPostConquestState(
            actor: new PlayerId(0),
            filler: new PlayerId(1),
            mode: GameMode.SecretMission,
            actorMission: mission,
            alaskaTroopsBeforeOccupy: 3,
            extraQualifyingTerritoryTroops: 2,
            pendingMinimumTroops: 1);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new OccupyCommand(state.Turn.CurrentPlayer, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.IsType<GameStatus.InProgress>(ok.State.Status);
        Assert.DoesNotContain(ok.Events, e => e is GameWon);
    }

    /// <summary>
    /// Moving enough troops into the conquered territory to satisfy its own
    /// threshold can still drain <c>pending.From</c> below the mission's own
    /// threshold, which must not falsely complete the mission either — the
    /// newly-qualifying conquered territory is offset by the now-disqualified
    /// source territory.
    /// </summary>
    [Fact]
    public void ExecuteOccupy_stays_InProgress_when_occupying_drains_the_source_territory_below_the_missions_own_threshold()
    {
        var mission = new OccupyTerritories(Count: 41, MinArmiesPerTerritory: 2);
        var state = BuildPostConquestState(
            actor: new PlayerId(0),
            filler: new PlayerId(1),
            mode: GameMode.SecretMission,
            actorMission: mission,
            alaskaTroopsBeforeOccupy: 3,
            extraQualifyingTerritoryTroops: 2,
            pendingMinimumTroops: 1);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new OccupyCommand(state.Turn.CurrentPlayer, 2));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.IsType<GameStatus.InProgress>(ok.State.Status);
        Assert.DoesNotContain(ok.Events, e => e is GameWon);
    }

    /// <summary>
    /// Classic's <see cref="ConquestVictoryRule"/> reads ownership only —
    /// zero <see cref="TerritoryState.Troops"/> references — so a non-winning
    /// occupy must stay a pure no-op: <see cref="GameStatus.InProgress"/> and
    /// exactly one <see cref="TerritoryOccupied"/> event, no
    /// <see cref="GameWon"/>.
    /// </summary>
    [Fact]
    public void ExecuteOccupy_emits_only_TerritoryOccupied_for_Classic_mode()
    {
        var state = BuildPostConquestState(
            actor: new PlayerId(0),
            filler: new PlayerId(1),
            mode: GameMode.Classic,
            actorMission: null,
            alaskaTroopsBeforeOccupy: 3,
            extraQualifyingTerritoryTroops: 1,
            pendingMinimumTroops: 1);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new OccupyCommand(state.Turn.CurrentPlayer, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.IsType<GameStatus.InProgress>(ok.State.Status);
        var raised = Assert.Single(ok.Events);
        Assert.IsType<TerritoryOccupied>(raised);
    }

    /// <summary>
    /// TwoPlayer's <see cref="TwoPlayerVictoryRule"/> reads elimination state
    /// only — zero territory/troop references — so a non-winning occupy must
    /// stay a pure no-op here too.
    /// </summary>
    [Fact]
    public void ExecuteOccupy_emits_only_TerritoryOccupied_for_TwoPlayer_mode()
    {
        var state = BuildPostConquestState(
            actor: new PlayerId(0),
            filler: new PlayerId(1),
            mode: GameMode.TwoPlayer,
            actorMission: null,
            alaskaTroopsBeforeOccupy: 3,
            extraQualifyingTerritoryTroops: 1,
            pendingMinimumTroops: 1);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new OccupyCommand(state.Turn.CurrentPlayer, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.IsType<GameStatus.InProgress>(ok.State.Status);
        var raised = Assert.Single(ok.Events);
        Assert.IsType<TerritoryOccupied>(raised);
    }

    /// <summary>
    /// Capital: <see cref="VictoryRules.For"/> returns <see langword="null"/>
    /// for <see cref="GameMode.Capital"/> (its real rule is roadmap item
    /// 5.3), so <c>ExecuteOccupy</c>'s <c>is {} modeVictoryRule</c> pattern
    /// must fail and skip the block entirely. Proven with a
    /// <see cref="RecordingVictoryRule"/> "trap" mapped to every OTHER mode:
    /// if <c>ExecuteOccupy</c> ever ignored <see cref="GameState.Mode"/> and
    /// fell back to some other rule regardless of the resolver's result for
    /// Capital, the trap's <see cref="RecordingVictoryRule.Calls"/> would
    /// become nonzero.
    /// </summary>
    [Fact]
    public void ExecuteOccupy_never_invokes_a_victory_rule_for_Capital_mode()
    {
        var state = BuildPostConquestState(
            actor: new PlayerId(0),
            filler: new PlayerId(1),
            mode: GameMode.Capital,
            actorMission: null,
            alaskaTroopsBeforeOccupy: 3,
            extraQualifyingTerritoryTroops: 1,
            pendingMinimumTroops: 1);
        var trap = new RecordingVictoryRule(new ConquestVictoryRule());
        Func<GameMode, IVictoryRule?> victoryRuleFor = mode => mode == GameMode.Capital ? null : trap;
        var engine = new GameEngine(new QueuedDiceRoller(), victoryRuleFor);

        var result = engine.Execute(state, new OccupyCommand(state.Turn.CurrentPlayer, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.IsType<GameStatus.InProgress>(ok.State.Status);
        Assert.Equal(0, trap.Calls);
    }

    /// <summary>
    /// <paramref name="attacker"/> owns every territory except
    /// <see cref="NorthwestTerritory"/> (<paramref name="defender"/>'s sole
    /// remaining territory), each with 1 troop except <see cref="Alaska"/>
    /// (the attacking territory, at 4), and holds <paramref name="mission"/>.
    /// Mirrors <c>SecretMissionWiringTests.BuildOneTerritoryFromVictoryState</c>.
    /// </summary>
    private static GameState BuildBoardClearingAttackState(PlayerId attacker, PlayerId defender, MissionCard mission)
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
            new PlayerState(attacker, [], false, 0, Mission: mission),
            new PlayerState(defender, [], false, 0)
        ];

        return new GameState(
            territories, players, new TurnState(attacker, TurnPhase.Attack), Deck.CreateStandard(), [], new GameStatus.InProgress(), Mode: GameMode.SecretMission);
    }

    /// <summary>
    /// A state already past a conquest, with a <see cref="PendingOccupation"/>
    /// armed on <see cref="Alaska"/> → <see cref="NorthwestTerritory"/> — the
    /// conquered territory sits at 0 troops, exactly as
    /// <c>ExecuteAttack</c> leaves it. <paramref name="actor"/> owns every
    /// territory except <see cref="Kamchatka"/> (owned by
    /// <paramref name="filler"/>, so the actor never already owns the whole
    /// board). Every non-Alaska/non-conquered actor-owned territory gets
    /// <paramref name="extraQualifyingTerritoryTroops"/> troops.
    /// </summary>
    private static GameState BuildPostConquestState(
        PlayerId actor,
        PlayerId filler,
        GameMode mode,
        MissionCard? actorMission,
        int alaskaTroopsBeforeOccupy,
        int extraQualifyingTerritoryTroops,
        int pendingMinimumTroops)
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>();
        foreach (var territory in WorldMap.Territories)
        {
            territories[territory.Id] = territory.Id switch
            {
                _ when territory.Id == Alaska => new TerritoryState(actor, alaskaTroopsBeforeOccupy),
                _ when territory.Id == NorthwestTerritory => new TerritoryState(actor, 0),
                _ when territory.Id == Kamchatka => new TerritoryState(filler, 1),
                _ => new TerritoryState(actor, extraQualifyingTerritoryTroops)
            };
        }

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(actor, [], false, 0, Mission: actorMission),
            new PlayerState(filler, [], false, 0)
        ];

        var turn = new TurnState(
            actor,
            TurnPhase.Attack,
            PendingOccupation: new PendingOccupation(Alaska, NorthwestTerritory, pendingMinimumTroops));

        return new GameState(territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress(), Mode: mode);
    }
}
