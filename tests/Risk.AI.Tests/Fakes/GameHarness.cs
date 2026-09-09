using Risk.Domain.Dice;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Setup;
using Risk.Engine.State;
using Risk.Engine.Views;

namespace Risk.AI.Tests.Fakes;

/// <summary>
/// Engine-backed replacement for <c>Risk.Tests</c>' <c>GameStateBuilder</c>
/// (which is <c>internal</c> without <c>InternalsVisibleTo</c> to
/// <c>Risk.AI.Tests</c>). Every state this fixture ever yields comes from
/// actually driving the real <see cref="GameEngine"/> via <see cref="FirstLegalBot"/>
/// — never from hand-assembling a <see cref="GameState"/> — so every state is
/// reachable by construction, the same guarantee <c>GameStateBuilder</c>
/// gives <c>Risk.Tests</c>.
/// </summary>
internal sealed class GameHarness
{
    private readonly Dictionary<PlayerId, BotMemory> _driverMemories;

    private GameHarness(GameState state, IGameEngine engine)
    {
        State = state;
        Engine = engine;
        _driverMemories = state.Players
            .Where(p => !p.IsNeutral)
            .ToDictionary(p => p.Id, _ => BotMemory.Empty);
    }

    public GameState State { get; private set; }

    public IGameEngine Engine { get; }

    /// <summary>
    /// Creates a fresh game via the real <see cref="GameSetup.Create"/>.
    /// Throws if <paramref name="playerCount"/>/<paramref name="mode"/> is an
    /// illegal combination — a fixture setup mistake, not something a test
    /// should ever need to assert on.
    /// </summary>
    public static GameHarness Start(GameMode mode, int playerCount, IDiceRoller dice)
    {
        var engine = new GameEngine(dice);
        var result = GameSetup.Create(playerCount, mode, dice);

        if (result is CommandResult<GameState, GameEvent>.Rejected rejected)
        {
            throw new InvalidOperationException(
                $"GameHarness.Start: setup rejected ({rejected.Error.Code}: {rejected.Error.Message}).");
        }

        var ok = (CommandResult<GameState, GameEvent>.Ok)result;
        return new GameHarness(ok.State, engine);
    }

    /// <summary>
    /// Drives the <see cref="TurnPhase.Claim"/> phase to completion via
    /// <see cref="FirstLegalBot"/> (Classic/Capital only). A no-op for modes
    /// that start directly in <see cref="TurnPhase.Setup"/> (SecretMission,
    /// TwoPlayer).
    /// </summary>
    public GameHarness FastForwardToSetup()
    {
        while (State.Turn.Phase == TurnPhase.Claim)
        {
            DriveOneCommand();
        }

        return this;
    }

    /// <summary>
    /// Drives Claim (if applicable) and Setup to completion, landing at
    /// <see cref="TurnPhase.SelectHeadquarters"/> — meaningful only for
    /// <see cref="GameMode.Capital"/>, the only mode with that phase.
    /// </summary>
    public GameHarness FastForwardToSelectHeadquarters()
    {
        FastForwardToSetup();

        while (State.Turn.Phase == TurnPhase.Setup)
        {
            DriveOneCommand();
        }

        return this;
    }

    /// <summary>
    /// Drives every phase before <see cref="TurnPhase.Reinforce"/> to
    /// completion — Claim (if applicable), Setup (including TwoPlayer's
    /// Phase A/B, design D8), and SelectHeadquarters (Capital only) — for
    /// every <see cref="GameMode"/>.
    /// </summary>
    public GameHarness FastForwardToFirstReinforce()
    {
        FastForwardToSetup();

        while (State.Turn.Phase is TurnPhase.Setup or TurnPhase.SelectHeadquarters)
        {
            DriveOneCommand();
        }

        return this;
    }

    /// <summary>
    /// Executes <paramref name="command"/> against the real engine. Throws
    /// with the <see cref="Risk.Domain.Errors.GameError"/>'s own text on
    /// <c>Rejected</c> — a test bug or a fixture bug, never something a test
    /// using this helper should need to handle gracefully.
    /// </summary>
    public GameHarness Apply(GameCommand command)
    {
        var result = Engine.Execute(State, command);

        if (result is CommandResult<GameState, GameEvent>.Rejected rejected)
        {
            throw new InvalidOperationException(
                $"GameHarness.Apply: command rejected ({rejected.Error.Code}: {rejected.Error.Message}).");
        }

        State = ((CommandResult<GameState, GameEvent>.Ok)result).State;
        return this;
    }

    public PlayerView ViewFor(PlayerId viewer) => Engine.Observe(State, viewer);

    /// <summary>
    /// Advances one command via <see cref="FirstLegalBot"/> for whoever's
    /// turn it currently is, threading every tracked player's
    /// <see cref="BotMemory"/> through <see cref="BotMemory.WithSeenActor"/>
    /// on every iteration (design D8's data-flow diagram) — this fixture
    /// needs the same seen-actor bookkeeping the production
    /// <see cref="BotTurnRunner"/> maintains, since <see cref="FirstLegalBot"/>'s
    /// own TwoPlayer Phase B detection depends on it exactly like
    /// <c>Decisions.SetupDecision</c>'s does.
    /// </summary>
    private void DriveOneCommand()
    {
        var actor = State.Turn.CurrentPlayer;

        foreach (var id in _driverMemories.Keys.ToArray())
        {
            _driverMemories[id] = _driverMemories[id].WithSeenActor(actor);
        }

        var bot = new FirstLegalBot(actor);
        var view = Engine.Observe(State, actor);
        var (command, decidedMemory) = bot.DecideNextCommand(view, _driverMemories[actor]);

        var result = Engine.Execute(State, command);

        if (result is CommandResult<GameState, GameEvent>.Rejected rejected)
        {
            throw new InvalidOperationException(
                "GameHarness fast-forward: FirstLegalBot issued a command the engine rejected " +
                $"({rejected.Error.Code}: {rejected.Error.Message}) — this indicates a bug in " +
                "FirstLegalBot, not in whatever this fixture is being used to test.");
        }

        var ok = (CommandResult<GameState, GameEvent>.Ok)result;
        _driverMemories[actor] = BotMemory.Fold(decidedMemory, actor, view, ok.Events);
        State = ok.State;
    }
}
