using Risk.Domain.Errors;
using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Engine.Views;
using Risk.Web.Models;
using Risk.Web.Services;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Services;

public class GameSessionServiceTests
{
    private static readonly IReadOnlyList<PlayerSetupRow> TwoValidRows =
    [
        new PlayerSetupRow("Ana", "#FF0000", false),
        new PlayerSetupRow("Beto", "#00FF00", false)
    ];

    [Fact]
    public void Start_WithValidRows_SetsStateAndPlayersAndRaisesChanged()
    {
        var session = new GameSessionService(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(2));
        var raised = false;
        session.Changed += () => raised = true;

        var result = session.Start(TwoValidRows, GameMode.TwoPlayer);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.True(session.IsStarted);
        Assert.NotNull(session.State);
        Assert.True(raised);
        Assert.Equal("Ana", session.ConfigFor(new PlayerId(0)).Name);
        Assert.Equal("Beto", session.ConfigFor(new PlayerId(1)).Name);
    }

    [Fact]
    public void Start_WithInvalidPlayerCount_LeavesStateNullAndDoesNotRaiseChanged()
    {
        // Rejected before dice is ever touched (1 player is illegal for the
        // default Classic mode), so an empty roller is safe here.
        var session = new GameSessionService(new FakeGameEngine(), new QueuedDiceRoller());
        var raised = false;
        session.Changed += () => raised = true;

        var result = session.Start([new PlayerSetupRow("Solo", "#FF0000", false)]);

        Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.False(session.IsStarted);
        Assert.Null(session.State);
        Assert.False(raised);
    }

    [Fact]
    public void Execute_Ok_MutatesStateAndRaisesChanged()
    {
        var engine = new FakeGameEngine();
        var session = new GameSessionService(engine, QueuedDiceRoller.ForRollOff(2));
        session.Start(TwoValidRows, GameMode.TwoPlayer);
        var stateAfterStart = session.State!;
        var newState = stateAfterStart with { TradesCompleted = 1 };
        engine.ExecuteResult = new CommandResult<GameState, GameEvent>.Ok(newState, []);
        var raised = false;
        session.Changed += () => raised = true;

        var command = new PlaceTroopsCommand(new PlayerId(0), new TerritoryId("Alaska"), 1);
        var result = session.Execute(command);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Same(newState, session.State);
        Assert.True(raised);
        Assert.Same(command, engine.LastCommand);
    }

    [Fact]
    public void Execute_Rejected_LeavesStateUnchangedAndDoesNotRaiseChanged()
    {
        var engine = new FakeGameEngine();
        var session = new GameSessionService(engine, QueuedDiceRoller.ForRollOff(2));
        session.Start(TwoValidRows, GameMode.TwoPlayer);
        var stateAfterStart = session.State!;
        engine.ExecuteResult = new CommandResult<GameState, GameEvent>.Rejected(
            new GameError(GameErrorCode.NotYourTurn, "not your turn"));
        var raised = false;
        session.Changed += () => raised = true;

        var result = session.Execute(new PlaceTroopsCommand(new PlayerId(0), new TerritoryId("Alaska"), 1));

        Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Same(stateAfterStart, session.State);
        Assert.False(raised);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var session = new GameSessionService(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(2));
        session.Start(TwoValidRows, GameMode.TwoPlayer);

        session.Reset();

        Assert.False(session.IsStarted);
        Assert.Null(session.State);
        Assert.Empty(session.Players);
    }

    [Theory]
    [InlineData(GameMode.TwoPlayer, 2)]
    [InlineData(GameMode.Classic, 3)]
    [InlineData(GameMode.Classic, 4)]
    [InlineData(GameMode.Classic, 5)]
    public void Start_ZipsRowsToPlayerIdInOrder_ForEveryValidPlayerCount(GameMode mode, int playerCount)
    {
        var rows = Enumerable.Range(0, playerCount)
            .Select(i => new PlayerSetupRow(
                $"Player{i}",
                PlayerPalette.Swatches[i % PlayerPalette.Swatches.Count],
                false))
            .ToArray();
        var session = new GameSessionService(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(playerCount));

        var result = session.Start(rows, mode);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        for (var i = 0; i < playerCount; i++)
        {
            var config = session.ConfigFor(new PlayerId(i));
            Assert.Equal(rows[i].Name, config.Name);
            Assert.Equal(rows[i].ColorHex, config.ColorHex);
        }
    }

    [Fact]
    public void Start_WithClassicMode_StartsInClaimPhase()
    {
        var rows = Enumerable.Range(0, 3)
            .Select(i => new PlayerSetupRow(
                $"Player{i}",
                PlayerPalette.Swatches[i % PlayerPalette.Swatches.Count],
                false))
            .ToArray();
        var session = new GameSessionService(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(3));

        var result = session.Start(rows, GameMode.Classic);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(TurnPhase.Claim, session.State!.Turn.Phase);
    }

    [Fact]
    public void Start_WithTwoPlayerMode_SynthesizesNeutralPlayerConfig()
    {
        var session = new GameSessionService(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(2));

        var result = session.Start(TwoValidRows, GameMode.TwoPlayer);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var neutral = session.State!.Players.Single(p => p.IsNeutral);
        var config = session.ConfigFor(neutral.Id);
        Assert.Equal(BoardColors.NeutralColor, config.ColorHex);
        Assert.False(config.IsAi);
        Assert.Equal(3, session.Players.Count);
    }

    [Fact]
    public void Start_WithClassicMode_DoesNotSynthesizeAnyNeutralPlayerConfig()
    {
        var rows = Enumerable.Range(0, 3)
            .Select(i => new PlayerSetupRow(
                $"Player{i}",
                PlayerPalette.Swatches[i % PlayerPalette.Swatches.Count],
                false))
            .ToArray();
        var session = new GameSessionService(new FakeGameEngine(), QueuedDiceRoller.ForRollOff(3));

        var result = session.Start(rows, GameMode.Classic);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(3, session.Players.Count);
        Assert.DoesNotContain(session.Players.Values, config => config.ColorHex == BoardColors.NeutralColor);
    }

    [Fact]
    public void Start_RejectsSixPlayersUnderClassicMode()
    {
        var rows = Enumerable.Range(0, 6)
            .Select(i => new PlayerSetupRow(
                $"Player{i}",
                PlayerPalette.Swatches[i % PlayerPalette.Swatches.Count],
                false))
            .ToArray();
        // Rejected before dice is ever touched (6 players is illegal for
        // Classic), so an empty roller is safe here.
        var session = new GameSessionService(new FakeGameEngine(), new QueuedDiceRoller());

        var result = session.Start(rows, GameMode.Classic);

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidPlayerCount, rejection.Error.Code);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Start_WithSecretMissionMode_SucceedsForEverySeatCountAndDealsEveryoneAMission(int playerCount)
    {
        var rows = Enumerable.Range(0, playerCount)
            .Select(i => new PlayerSetupRow(
                $"Player{i}",
                PlayerPalette.Swatches[i % PlayerPalette.Swatches.Count],
                false))
            .ToArray();
        // SecretMissionSetup never rolls dice (only Classic's TurnOrder.DetermineFirst does).
        var session = new GameSessionService(new FakeGameEngine(), new QueuedDiceRoller());

        var result = session.Start(rows, GameMode.SecretMission);

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        for (var i = 0; i < playerCount; i++)
        {
            Assert.NotNull(session.State!.Players.Single(p => p.Id == new PlayerId(i)).Mission);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void Start_RejectsOutOfRangePlayerCountsUnderSecretMissionMode(int playerCount)
    {
        var rows = Enumerable.Range(0, playerCount)
            .Select(i => new PlayerSetupRow(
                $"Player{i}",
                PlayerPalette.Swatches[i % PlayerPalette.Swatches.Count],
                false))
            .ToArray();
        var session = new GameSessionService(new FakeGameEngine(), new QueuedDiceRoller());

        var result = session.Start(rows, GameMode.SecretMission);

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidPlayerCount, rejection.Error.Code);
    }

    private static IReadOnlyList<PlayerSetupRow> ThreeValidRows =>
    [
        new PlayerSetupRow("Ana", "#FF0000", false),
        new PlayerSetupRow("Beto", "#00FF00", false),
        new PlayerSetupRow("Carla", "#0000FF", false)
    ];

    /// <summary>
    /// Builds a <see cref="PlayerView"/> with only <see cref="PlayerView.OwnEffectiveMission"/>
    /// meaningfully populated — the rest are structurally-required but
    /// unused-by-<c>WinnerMission</c> fields, kept minimal since this test
    /// class only exercises <see cref="GameSessionService"/>'s own wiring
    /// (whether/who it calls <c>Observe</c> for), not <c>Observe</c>'s own
    /// mission-resolution correctness — that is covered end-to-end by
    /// <c>Risk.Tests.Rules.MissionResolutionTests</c> and
    /// <c>Risk.Tests.Engine.GameEngineObserveTests</c> (design 3.4-D1/D2).
    /// </summary>
    private static PlayerView ViewWithMission(GameState state, MissionCard? mission) => new(
        state.Territories,
        [],
        new Dictionary<PlayerId, int>(),
        state.Turn,
        null,
        new Dictionary<PlayerId, TerritoryId>(),
        mission);

    [Fact]
    public void WinnerMission_ReturnsNull_WhileInProgress()
    {
        var engine = new FakeGameEngine();
        var session = new GameSessionService(engine, new QueuedDiceRoller());
        session.Start(ThreeValidRows, GameMode.SecretMission);

        var result = session.WinnerMission();

        Assert.Null(result);
        // Design 3.4-D4: WinnerMission must be structurally incapable of
        // leaking a live player's mission — it must never even call Observe
        // while the game is InProgress.
        Assert.Null(engine.LastObserveViewer);
    }

    [Fact]
    public void WinnerMission_ReturnsWinnersEffectiveMission_OnceWon()
    {
        var engine = new FakeGameEngine();
        var session = new GameSessionService(engine, new QueuedDiceRoller());
        session.Start(ThreeValidRows, GameMode.SecretMission);
        var winner = session.State!.Turn.CurrentPlayer;
        var wonState = session.State! with { Status = new GameStatus.Won(winner) };
        // The self-target EliminateArmy substitution (design 3.4-D1) is what
        // Observe would have already resolved by the time WinnerMission sees
        // it — asserted here as the OwnEffectiveMission the fake hands back,
        // proving WinnerMission passes it through unmodified rather than
        // re-deriving or reading the raw dealt PlayerState.Mission.
        var effectiveMission = new OccupyTerritories(24, MinArmiesPerTerritory: 1);
        engine.ExecuteResult = new CommandResult<GameState, GameEvent>.Ok(wonState, []);
        engine.ObserveResult = ViewWithMission(wonState, effectiveMission);
        session.Execute(new EndPhaseCommand(winner));

        var result = session.WinnerMission();

        Assert.Same(effectiveMission, result);
        Assert.Equal(winner, engine.LastObserveViewer);
    }

    [Fact]
    public void WinnerMission_ReturnsNull_AfterReset()
    {
        var engine = new FakeGameEngine();
        var session = new GameSessionService(engine, new QueuedDiceRoller());
        session.Start(ThreeValidRows, GameMode.SecretMission);
        var winner = session.State!.Turn.CurrentPlayer;
        var wonState = session.State! with { Status = new GameStatus.Won(winner) };
        engine.ExecuteResult = new CommandResult<GameState, GameEvent>.Ok(wonState, []);
        engine.ObserveResult = ViewWithMission(wonState, new OccupyTerritories(24, MinArmiesPerTerritory: 1));
        session.Execute(new EndPhaseCommand(winner));
        Assert.NotNull(session.WinnerMission());

        session.Reset();

        Assert.Null(session.WinnerMission());
    }

    [Fact]
    public void WinnerMission_ReturnsNull_InWonClassicGame()
    {
        var engine = new FakeGameEngine();
        var session = new GameSessionService(engine, QueuedDiceRoller.ForRollOff(3));
        session.Start(ThreeValidRows, GameMode.Classic);
        var winner = session.State!.Turn.CurrentPlayer;
        var wonState = session.State! with { Status = new GameStatus.Won(winner) };
        engine.ExecuteResult = new CommandResult<GameState, GameEvent>.Ok(wonState, []);
        // Classic never deals missions, so a real Observe would report null
        // here too — mirrored by the fake for this wiring-only test.
        engine.ObserveResult = ViewWithMission(wonState, null);
        session.Execute(new EndPhaseCommand(winner));

        var result = session.WinnerMission();

        Assert.Null(result);
        Assert.Equal(winner, engine.LastObserveViewer);
    }
}
