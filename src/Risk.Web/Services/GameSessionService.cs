using Risk.Domain.Dice;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Setup;
using Risk.Engine.State;
using Risk.Engine.Views;
using Risk.Web.Models;

namespace Risk.Web.Services;

/// <summary>
/// The composition root's single stateful seam between Razor components and
/// <see cref="IGameEngine"/>. Registered scoped (one per Blazor Server
/// circuit = one hot-seat game); every mutator assigns <see cref="State"/>
/// and <see cref="LastEvents"/> and raises <see cref="Changed"/> on
/// success, and leaves both untouched on rejection so components can
/// pattern-match one dispatch idiom throughout the UI.
/// </summary>
public sealed class GameSessionService(IGameEngine engine, IDiceRoller dice)
{
    public GameState? State { get; private set; }

    public IReadOnlyDictionary<PlayerId, PlayerConfig> Players { get; private set; } =
        new Dictionary<PlayerId, PlayerConfig>();

    public IReadOnlyList<GameEvent> LastEvents { get; private set; } = [];

    public bool IsStarted => State is not null;

    public event Action? Changed;

    /// <summary>
    /// Starts a new game from the setup screen's rows: wraps
    /// <see cref="GameSetup.Create"/> and, on success, zips
    /// <paramref name="rows"/> to the implicit <c>PlayerId(0..N-1)</c>
    /// order that <see cref="GameSetup.Create"/> assigns.
    /// </summary>
    /// <param name="rows">The configured players, in seating order.</param>
    /// <param name="mode">
    /// Defaults to <see cref="GameMode.Classic"/> as a placeholder until a
    /// mode-selector UI ships (roadmap item 2.2). This is a known, accepted
    /// temporary regression: Risk.Web currently cannot start a 2-player game,
    /// since <see cref="GameMode.Classic"/> requires 3-5 players.
    /// </param>
    public CommandResult<GameState, GameEvent> Start(IReadOnlyList<PlayerSetupRow> rows, GameMode mode = GameMode.Classic)
    {
        var result = GameSetup.Create(rows.Count, mode, dice);

        if (result is CommandResult<GameState, GameEvent>.Ok ok)
        {
            State = ok.State;
            LastEvents = ok.Events;
            Players = rows
                .Select((row, index) => new PlayerConfig(new PlayerId(index), row.Name, row.ColorHex, row.IsAi))
                .ToDictionary(config => config.Id);
            Changed?.Invoke();
        }

        return result;
    }

    public CommandResult<GameState, GameEvent> Execute(GameCommand command)
    {
        if (State is null)
        {
            throw new InvalidOperationException("GameSessionService.Execute was called before Start.");
        }

        var result = engine.Execute(State, command);

        if (result is CommandResult<GameState, GameEvent>.Ok ok)
        {
            State = ok.State;
            LastEvents = ok.Events;
            Changed?.Invoke();
        }

        return result;
    }

    /// <summary>
    /// The current player's redacted view: their own hand in full, everyone
    /// else's hand reduced to a count. Throws if called before <see cref="Start"/>
    /// — a programmer error, not a rule violation.
    /// </summary>
    public PlayerView ObserveCurrentPlayer()
    {
        if (State is null)
        {
            throw new InvalidOperationException("GameSessionService.ObserveCurrentPlayer was called before Start.");
        }

        return engine.Observe(State, State.Turn.CurrentPlayer);
    }

    public PlayerConfig ConfigFor(PlayerId id) => Players[id];

    /// <summary>Clears the session back to its pre-<see cref="Start"/> state, for the victory screen's "new game".</summary>
    public void Reset()
    {
        State = null;
        Players = new Dictionary<PlayerId, PlayerConfig>();
        LastEvents = [];
    }
}
