using Risk.Domain.Dice;
using Risk.Domain.Missions;
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
    /// <summary>
    /// A safety valve for the fast-forward loops below, mirroring
    /// <see cref="BotTurnRunner"/>'s own <c>maxCommands</c> budgets: sized
    /// comfortably above anything a legitimate fast-forward could ever need
    /// (worst case, Classic/5-players: ~42 Claim commands + ~85 Setup
    /// commands ≈ 130), not <see cref="Scoring.BotWeights.MaxCommandsPerGame"/>'s
    /// 250,000, which is sized for a full game, not a setup fast-forward.
    /// Configurable per-instance (see <see cref="Start"/>'s optional
    /// parameter) so a test can deliberately lower it to prove the cap fires
    /// without needing a genuinely non-terminating command sequence — which,
    /// verified directly against every Claim/Setup/SelectHeadquarters
    /// command's engine-side validation, does not exist for a LEGAL command
    /// today (every one of them consumes a strictly-decreasing resource:
    /// remaining unclaimed territories, a player's own troop pool, or a
    /// one-shot headquarters pick). This cap exists for the FUTURE: a
    /// not-yet-written `Decisions.*` path (e.g. Capital's
    /// `SelectHeadquarters`, SecretMission's Setup) could still introduce a
    /// legal-but-non-progressing bug that a bare `while` loop would hang on
    /// forever instead of failing loudly.
    /// </summary>
    private const int DefaultMaxFastForwardIterations = 500;

    private readonly Dictionary<PlayerId, BotMemory> _driverMemories;
    private readonly int _maxFastForwardIterations;

    private GameHarness(GameState state, IGameEngine engine, int maxFastForwardIterations)
    {
        State = state;
        Engine = engine;
        _maxFastForwardIterations = maxFastForwardIterations;
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
    /// should ever need to assert on. <paramref name="maxFastForwardIterations"/>
    /// is test-only tuning for the fast-forward loops below; production-style
    /// callers never need to override the default.
    /// </summary>
    public static GameHarness Start(
        GameMode mode,
        int playerCount,
        IDiceRoller dice,
        int maxFastForwardIterations = DefaultMaxFastForwardIterations)
    {
        var engine = new GameEngine(dice);
        var result = GameSetup.Create(playerCount, mode, dice);

        if (result is CommandResult<GameState, GameEvent>.Rejected rejected)
        {
            throw new InvalidOperationException(
                $"GameHarness.Start: setup rejected ({rejected.Error.Code}: {rejected.Error.Message}).");
        }

        var ok = (CommandResult<GameState, GameEvent>.Ok)result;
        return new GameHarness(ok.State, engine, maxFastForwardIterations);
    }

    /// <summary>
    /// Drives the <see cref="TurnPhase.Claim"/> phase to completion via
    /// <see cref="FirstLegalBot"/> (Classic/Capital only). A no-op for modes
    /// that start directly in <see cref="TurnPhase.Setup"/> (SecretMission,
    /// TwoPlayer).
    /// </summary>
    public GameHarness FastForwardToSetup()
    {
        DriveWhile(phase => phase == TurnPhase.Claim, "Setup (draining Claim)");
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
        DriveWhile(phase => phase == TurnPhase.Setup, "SelectHeadquarters (draining Setup)");
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
        DriveWhile(phase => phase is TurnPhase.Setup or TurnPhase.SelectHeadquarters, "Reinforce (draining Setup/SelectHeadquarters)");
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
    /// Overrides the dealt <see cref="PlayerState.Mission"/> for the given seats — used
    /// by SecretMission tests that need guaranteed per-archetype coverage rather than
    /// whatever <see cref="Risk.Engine.Modes.SecretMissionSetupStrategy"/>'s <c>Random.Shared</c>
    /// deal happens to produce (design D9's random-board invariant testing still applies to
    /// territory dealing and dice — only the mission assignment is forced here). Only
    /// <see cref="PlayerState.Mission"/> changes; every other field (including the
    /// randomly-dealt territories/troops) is untouched. Safe to call any time before the
    /// game is driven, since <c>Observe</c> re-resolves <c>OwnEffectiveMission</c> from
    /// <see cref="PlayerState.Mission"/> on every call rather than caching it.
    /// </summary>
    public GameHarness WithMissions(IReadOnlyDictionary<PlayerId, MissionCard> missions)
    {
        State = State with
        {
            Players = State.Players
                .Select(p => missions.TryGetValue(p.Id, out var mission) ? p with { Mission = mission } : p)
                .ToArray()
        };
        return this;
    }

    /// <summary>
    /// Drives <see cref="DriveOneCommand"/> while <paramref name="shouldContinue"/>
    /// holds on the current <see cref="TurnState.Phase"/>, capped at
    /// <see cref="_maxFastForwardIterations"/> so a non-progressing legal
    /// command loop (see <see cref="DefaultMaxFastForwardIterations"/>'s
    /// remarks) fails loudly and immediately instead of hanging the test
    /// process.
    /// </summary>
    private void DriveWhile(Func<TurnPhase, bool> shouldContinue, string targetPhaseDescription)
    {
        var iterations = 0;

        while (shouldContinue(State.Turn.Phase))
        {
            if (iterations++ >= _maxFastForwardIterations)
            {
                throw new InvalidOperationException(
                    $"GameHarness exceeded {_maxFastForwardIterations} iterations fast-forwarding to " +
                    $"{targetPhaseDescription} — likely a non-progressing legal command loop (a " +
                    "FirstLegalBot decision that never advances the phase).");
            }

            DriveOneCommand();
        }
    }

    /// <summary>
    /// Advances one command via <see cref="FirstLegalBot"/> for whoever's
    /// turn it currently is. Delegates the actual observe/decide/execute/fold
    /// sequence to <see cref="BotTurnStep.Advance"/> — the same step
    /// <see cref="BotTurnRunner"/> uses, including its <see cref="BotMemory.WithSeenActor"/>
    /// threading (design D8) — so this fixture's driving loop and the
    /// production runner can never silently drift apart.
    /// </summary>
    private void DriveOneCommand()
    {
        var actor = State.Turn.CurrentPlayer;
        var bot = new FirstLegalBot(actor);

        var (result, _, nextState) = BotTurnStep.Advance(Engine, State, actor, bot, _driverMemories);

        if (result is CommandResult<GameState, GameEvent>.Rejected rejected)
        {
            throw new InvalidOperationException(
                "GameHarness fast-forward: FirstLegalBot issued a command the engine rejected " +
                $"({rejected.Error.Code}: {rejected.Error.Message}) — this indicates a bug in " +
                "FirstLegalBot, not in whatever this fixture is being used to test.");
        }

        State = nextState;
    }
}
