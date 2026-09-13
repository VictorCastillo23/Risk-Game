using Risk.AI;
using Risk.Domain.Dice;
using Risk.Domain.Errors;
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
/// One networked game: the server-authoritative session every device's
/// circuit renders from. Unlike <see cref="GameSessionService"/> (scoped
/// per circuit = one hot-seat game), this is a singleton per join code,
/// held by <see cref="NetworkGameRegistry"/> and shared across circuits.
///
/// Seats are claimed pre-start (<see cref="ClaimSeat"/>), the host starts
/// once the lobby is full (<see cref="Start"/>), and devices bind to seats
/// (<see cref="Join"/>/<see cref="Leave"/>) — including rebinds after a
/// reconnect. Every mutation raises <see cref="Changed"/> so each circuit
/// re-renders (server-side fan-out over the circuits browsers already
/// hold; no wire-level hub — see the multiplayer design Correction).
///
/// Rule authority stays in <see cref="IGameEngine"/>: <see cref="Dispatch"/>
/// adds exactly one server-side gate (actor must be the current player)
/// before delegating, so a tampered client can never act out of turn.
///
/// AI seats (Phase 5) resolve server-side: after every accepted human turn
/// — and once at <see cref="Start"/> in case a bot opens — consecutive bot
/// turns drain through <see cref="BotTurnRunner"/> until a human is current
/// again. A bot illegality hard-fails into <see cref="AiFailure"/> (state
/// untouched, no retry), mirroring hot-seat AI seats' contract.
/// </summary>
public sealed class NetworkGameSession(
    IGameEngine engine,
    IDiceRoller dice,
    JoinCode code,
    Func<PlayerId, IBotPlayer>? botFactory = null)
{
    public JoinCode Code { get; } = code;

    public GameState? State { get; private set; }

    public IReadOnlyList<PlayerSeat> Seats { get; private set; } = [];

    public IReadOnlyList<GameEvent> LastEvents { get; private set; } = [];

    /// <summary>Null while every bot turn completes cleanly (see <see cref="AiTurnFailure"/>).</summary>
    public AiTurnFailure? AiFailure { get; private set; }

    public bool IsStarted => State is not null;

    public event Action? Changed;

    private readonly BotTurnRunner _runner = new(engine);
    private readonly Func<PlayerId, IBotPlayer> _botFactory = botFactory ?? (id => new BotPlayer(id));
    private Dictionary<PlayerId, IBotPlayer> _bots = new();
    private Dictionary<PlayerId, BotMemory> _memories = new();

    /// <summary>
    /// Claims the next seat; the first player claim is the host. Seated
    /// players get engine-stable ids (<c>PlayerId</c> = non-spectator claim
    /// count, so <see cref="Start"/>'s <c>0..N-1</c> zip holds no matter how
    /// many spectators interleave); spectators get negative ids that can
    /// never equal <c>Turn.CurrentPlayer</c>, so <see cref="Dispatch"/>
    /// rejects their commands by construction. Pre-start only for players;
    /// spectators may join anytime (including mid-game).
    /// </summary>
    public PlayerId ClaimSeat(string name, string colorHex, string? connectionId, bool isAi = false, bool isSpectator = false)
    {
        if (IsStarted && !isSpectator)
        {
            throw new InvalidOperationException("NetworkGameSession.ClaimSeat was called after Start.");
        }

        var id = isSpectator
            ? new PlayerId(-Seats.Count(s => s.IsSpectator) - 1)
            : new PlayerId(Seats.Count(s => !s.IsSpectator));
        var seat = new PlayerSeat(
            new PlayerConfig(id, name, colorHex, isAi),
            connectionId,
            IsHost: !isSpectator && Seats.All(s => !s.IsHost),
            IsConnected: connectionId is not null,
            IsSpectator: isSpectator);
        Seats = [.. Seats, seat];
        Changed?.Invoke();
        return id;
    }

    /// <summary>
    /// Builds engine players from the claimed non-spectator seats in claim
    /// order (their <c>PlayerConfig.Id</c> already zips <c>0..N-1</c> by
    /// <see cref="ClaimSeat"/> construction, like hot-seat <c>Start</c>). On success
    /// assigns <see cref="State"/> and raises <see cref="Changed"/>; on
    /// rejection leaves everything untouched.
    /// </summary>
    public CommandResult<GameState, GameEvent> Start(GameMode mode)
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("NetworkGameSession.Start was called twice.");
        }

        var rows = Seats
            .Where(s => !s.IsSpectator)
            .Select(s => new PlayerSetupRow(s.Config.Name, s.Config.ColorHex, s.Config.IsAi))
            .ToList();

        var result = GameSetup.Create(rows.Count, mode, dice);

        if (result is CommandResult<GameState, GameEvent>.Ok ok)
        {
            State = ok.State;
            LastEvents = ok.Events;
            AiFailure = null;

            // Same neutral-army synthesized config as hot-seat Start:
            // TwoPlayer's neutral is GameSetup.Create's own player, never a
            // claimed seat, so it needs a display config or the board falls
            // back to the unknown-owner color for it.
            if (ok.State.Players.SingleOrDefault(p => p.IsNeutral) is { } neutral)
            {
                Seats = [.. Seats, new PlayerSeat(
                    new PlayerConfig(neutral.Id, "Ejército neutral", BoardColors.NeutralColor, IsAi: false),
                    ConnectionId: null, IsHost: false, IsConnected: false, IsSpectator: false)];
            }

            RebuildBotRegistry();
            AdvanceAiTurns();
            Changed?.Invoke();
        }

        return result;
    }

    /// <summary>
    /// Restores a persisted snapshot as a fresh table (used by "open saved
    /// game as networked room"): assigns <see cref="State"/>, rebuilds
    /// seats from the snapshot configs (all unbound — players rejoin for a
    /// new code), first config hosts. Fresh sessions only.
    /// </summary>
    public void Restore(GameState state, IReadOnlyList<PlayerConfig> players)
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("NetworkGameSession.Restore was called on a started session.");
        }

        State = state;
        LastEvents = [];
        AiFailure = null;
        Seats = players
            .Select((p, i) => new PlayerSeat(
                p, ConnectionId: null, IsHost: i == 0, IsConnected: false, IsSpectator: false))
            .ToList();
        RebuildBotRegistry();
        Changed?.Invoke();
    }

    /// <summary>
    /// Binds a circuit to an existing seat (first join or reconnect
    /// rebind). Returns false for an unknown seat; never creates seats.
    /// A human non-spectator join claims a vacant host role (nobody
    /// connected when the previous host left).
    /// </summary>
    public bool Join(PlayerId seat, string connectionId)
    {
        var index = Seats.ToList().FindIndex(s => s.Config.Id == seat);
        if (index < 0)
        {
            return false;
        }

        var seats = Seats.ToList();
        seats[index] = seats[index] with { ConnectionId = connectionId, IsConnected = true };
        if (!seats[index].IsSpectator && !seats[index].Config.IsAi && seats.All(s => !s.IsHost))
        {
            seats[index] = seats[index] with { IsHost = true };
        }

        Seats = seats;
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Frees a seat's circuit binding (disconnect); the seat itself
    /// survives so a reconnect can rebind it. A departing host promotes
    /// the first still-connected human (lobby control must live
    /// somewhere); with nobody connected the role stays vacant until the
    /// next human join. Unknown seats are a silent no-op.
    /// </summary>
    public void Leave(PlayerId seat)
    {
        var index = Seats.ToList().FindIndex(s => s.Config.Id == seat);
        if (index < 0)
        {
            return;
        }

        var seats = Seats.ToList();
        var wasHost = seats[index].IsHost;
        // A disconnect always drops the host role with the circuit — a
        // disconnected seat must never keep holding it (that produced two
        // simultaneous hosts: the leaver plus the promoted heir).
        seats[index] = seats[index] with { ConnectionId = null, IsConnected = false, IsHost = false };
        if (wasHost)
        {
            var heir = seats.FindIndex(s => s.IsConnected && !s.IsSpectator && !s.Config.IsAi);
            if (heir >= 0)
            {
                seats[heir] = seats[heir] with { IsHost = true };
            }
        }

        Seats = seats;
        Changed?.Invoke();
    }

    /// <summary>
    /// Server-side turn gate + engine delegation. A command whose actor is
    /// not <c>Turn.CurrentPlayer</c> is rejected as
    /// <see cref="GameErrorCode.NotYourTurn"/> WITHOUT touching the engine
    /// (the fake in tests throws when called, proving the gate). Accepted
    /// commands behave exactly like hot-seat <c>Execute</c>.
    /// </summary>
    public CommandResult<GameState, GameEvent> Dispatch(GameCommand command)
    {
        if (State is null)
        {
            throw new InvalidOperationException("NetworkGameSession.Dispatch was called before Start.");
        }

        AiFailure = null;

        if (command.Actor != State.Turn.CurrentPlayer)
        {
            return new CommandResult<GameState, GameEvent>.Rejected(
                new GameError(GameErrorCode.NotYourTurn, "Only the current player can act."));
        }

        var result = engine.Execute(State, command);

        if (result is CommandResult<GameState, GameEvent>.Ok ok)
        {
            State = ok.State;
            LastEvents = ok.Events;
            MarkSeenByAllBots(command.Actor);
            Changed?.Invoke();
            AdvanceAiTurns();
        }

        return result;
    }

    /// <summary>
    /// A single seat's redacted view (own hand full, others count-only).
    /// The caller — one circuit per seat — only ever requests its own;
    /// routing one view per connection is what keeps hidden information
    /// hidden (design D4).
    /// </summary>
    public PlayerView Observe(PlayerId player)
    {
        if (State is null)
        {
            throw new InvalidOperationException("NetworkGameSession.Observe was called before Start.");
        }

        return engine.Observe(State, player);
    }

    private void RebuildBotRegistry()
    {
        _bots = Seats
            .Where(s => s.Config.IsAi && !s.IsSpectator)
            .ToDictionary(s => s.Config.Id, s => _botFactory(s.Config.Id));
        _memories = _bots.Keys.ToDictionary(id => id, _ => BotMemory.Empty);
    }

    private bool IsAiSeat(PlayerId id) => _bots.ContainsKey(id);

    /// <summary>
    /// Cross-seat visibility backfill: <see cref="BotTurnRunner.RunTurn"/>
    /// builds a single-entry memory map per call, so without this a bot
    /// would only ever record seeing ITSELF — never the other real seats —
    /// which stalls TwoPlayer Setup Phase B exactly like hot-seat AI seats
    /// found before (their <c>MarkSeenByAllBots</c>; same fix, same reason).
    /// Called for every accepted actor (human or bot) before bot turns run.
    /// </summary>
    private void MarkSeenByAllBots(PlayerId actor)
    {
        foreach (var id in _bots.Keys.ToList())
        {
            _memories[id] = _memories[id].WithSeenActor(actor);
        }
    }

    /// <summary>
    /// Drains consecutive bot turns until a human (or victory) is current.
    /// Each completed bot turn adopts state/memories, scopes
    /// <see cref="LastEvents"/> to that turn's delta, and raises
    /// <see cref="Changed"/> so devices animate bot-by-bot. A bot
    /// illegality or budget exhaustion hard-stops into <see cref="AiFailure"/>
    /// with state untouched and no retry — the Risk.AI contract, surfaced.
    /// </summary>
    private void AdvanceAiTurns()
    {
        var guard = 0;
        while (State is { Status: not GameStatus.Won } state && IsAiSeat(state.Turn.CurrentPlayer))
        {
            // Every Completed turn advances play, so this always terminates;
            // the cap is pure paranoia against a future non-advancing loop.
            if (++guard > 64)
            {
                AiFailure = AiTurnFailure.BudgetExhausted(state.Turn.CurrentPlayer);
                Changed?.Invoke();
                return;
            }

            var current = state.Turn.CurrentPlayer;
            var logCount = state.Log.Count;
            MarkSeenByAllBots(current);

            switch (_runner.RunTurn(state, _bots[current], _memories[current]))
            {
                case BotRunResult.Completed completed:
                    foreach (var (id, memory) in completed.Memories)
                    {
                        _memories[id] = memory;
                    }

                    State = completed.State;
                    LastEvents = completed.State.Log.Skip(logCount).ToList();
                    Changed?.Invoke();
                    break;

                case BotRunResult.Rejected rejected:
                    foreach (var (id, memory) in rejected.Memories)
                    {
                        _memories[id] = memory;
                    }

                    AiFailure = AiTurnFailure.Rejected(current, rejected.Command, rejected.Error);
                    Changed?.Invoke();
                    return;

                case BotRunResult.Exhausted exhausted:
                    foreach (var (id, memory) in exhausted.Memories)
                    {
                        _memories[id] = memory;
                    }

                    State = exhausted.State;
                    LastEvents = exhausted.State.Log.Skip(logCount).ToList();
                    AiFailure = AiTurnFailure.BudgetExhausted(current);
                    Changed?.Invoke();
                    return;
            }
        }
    }
}
