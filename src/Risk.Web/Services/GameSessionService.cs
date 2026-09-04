using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
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
using Risk.Web.Models;
using Risk.Web.Persistence;

namespace Risk.Web.Services;

/// <summary>
/// Which of the three outcomes <see cref="GameSessionService.SaveAsync"/>
/// resolved to: no signed-in user (the caller must authenticate first —
/// user-accounts' "Save triggers authentication"), a brand-new row, or an
/// existing row that was overwritten (game-persistence's "second save
/// overwrites the first"). PR6 uses <see cref="Overwritten"/> to decide
/// whether a confirm-before-overwrite prompt was warranted in hindsight, or
/// (in a future pre-check flow) combines this with <c>GetSummaryAsync</c> to
/// ask before saving.
/// </summary>
public enum SaveOutcome
{
    NotAuthenticated,
    Created,
    Overwritten,
}

/// <summary>
/// The composition root's single stateful seam between Razor components and
/// <see cref="IGameEngine"/>. Registered scoped (one per Blazor Server
/// circuit = one hot-seat game); every mutator assigns <see cref="State"/>
/// and <see cref="LastEvents"/> and raises <see cref="Changed"/> on
/// success, and leaves both untouched on rejection so components can
/// pattern-match one dispatch idiom throughout the UI.
///
/// Persistence (PR5) is entirely optional/nullable-by-default (spec's
/// "anonymous play unaffected"): <see cref="OwnerUserId"/> stays
/// <see langword="null"/>, and no <see cref="IGameStore"/> call is ever made,
/// unless a save/resume is actually invoked via <see cref="SaveAsync"/>/
/// <see cref="ResumeAsync"/>. The signed-in user's id is resolved from
/// <paramref name="authStateProvider"/>'s server-side principal (design D4)
/// — no component ever passes an id in directly.
/// </summary>
public sealed class GameSessionService(
    IGameEngine engine,
    IDiceRoller dice,
    IGameStore store,
    AuthenticationStateProvider authStateProvider)
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
    /// Which <see cref="GameMode"/> to start. Defaults to
    /// <see cref="GameMode.Classic"/>, matching <c>Setup.razor</c>'s mode
    /// dropdown default (roadmap item 2.2).
    /// </param>
    public CommandResult<GameState, GameEvent> Start(IReadOnlyList<PlayerSetupRow> rows, GameMode mode = GameMode.Classic)
    {
        var result = GameSetup.Create(rows.Count, mode, dice);

        if (result is CommandResult<GameState, GameEvent>.Ok ok)
        {
            State = ok.State;
            LastEvents = ok.Events;
            var players = rows
                .Select((row, index) => new PlayerConfig(new PlayerId(index), row.Name, row.ColorHex, row.IsAi))
                .ToDictionary(config => config.Id);

            // Design D2: GameMode.TwoPlayer's neutral third army is
            // GameSetup.Create's own player, not one of the setup screen's
            // rows — it never gets a PlayerSetupRow, so it needs its own
            // synthesized PlayerConfig here or BoardSvg/PhaseIndicator would
            // fall back to BoardColors.UnknownOwnerColor for it. IsAi: false
            // — the neutral never takes a turn, it's only ever a placement
            // target during Setup Phase B, chosen by a human.
            if (ok.State.Players.SingleOrDefault(p => p.IsNeutral) is { } neutral)
            {
                players[neutral.Id] = new PlayerConfig(neutral.Id, "Ejército neutral", BoardColors.NeutralColor, IsAi: false);
            }

            Players = players;
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

    /// <summary>
    /// The winner's effective mission, for <c>VictoryScreen</c>'s
    /// rules-mandated reveal (reglasrisk.md:286). Returns <see langword="null"/>
    /// while the game is <see cref="GameStatus.InProgress"/>, so this method
    /// is structurally incapable of leaking a live player's hidden mission —
    /// the reveal gate is the return type's own precondition, not a
    /// convention the caller has to honour. Also <see langword="null"/> in
    /// every non-<see cref="GameMode.SecretMission"/> mode, because no
    /// mission was ever dealt, and reports the resolved (not the dealt)
    /// mission via <see cref="IGameEngine.Observe"/>, exactly like
    /// <see cref="ObserveCurrentPlayer"/> — never reads
    /// <c>GameState.Players[x].Mission</c> directly (design 3.4-D4).
    /// </summary>
    public MissionCard? WinnerMission() =>
        State is { Status: GameStatus.Won won } state
            ? engine.Observe(state, won.Winner).OwnEffectiveMission
            : null;

    public PlayerConfig ConfigFor(PlayerId id) => Players[id];

    /// <summary>
    /// Clears the session back to its pre-<see cref="Start"/> state, for the
    /// victory screen's "new game". Also clears <see cref="OwnerUserId"/> —
    /// a freshly-reset session has no game left to be owned by anyone. Now
    /// raises <see cref="Changed"/> (previous versions did not); verified
    /// safe against <c>Game.razor</c>'s <c>OnSessionChanged</c> handler,
    /// which is entirely null-safe against a just-reset session (reads
    /// <c>Session.State?.</c> and <c>Session.LastEvents</c>'s now-empty
    /// list) — see PR5's apply-progress for the full trace.
    /// </summary>
    public void Reset()
    {
        State = null;
        Players = new Dictionary<PlayerId, PlayerConfig>();
        LastEvents = [];
        OwnerUserId = null;
        Changed?.Invoke();
    }

    /// <summary>
    /// Set once a save or resume succeeds for this session; <see langword="null"/>
    /// for an anonymous or not-yet-persisted game (spec's "anonymous play
    /// unaffected").
    /// </summary>
    public string? OwnerUserId { get; private set; }

    /// <summary>Whether this session is currently associated with a persisted save.</summary>
    public bool HasPersistedSave => OwnerUserId is not null;

    /// <summary>
    /// Builds a <see cref="GameSnapshot"/> from the current <see cref="State"/>
    /// and <see cref="Players"/>, for <see cref="SaveAsync"/> or a caller
    /// that needs to stash it (e.g. <c>ProtectedSessionStorage</c> for the
    /// anonymous-save flow, design D2). Throws if called before <see cref="Start"/> —
    /// a programmer error, matching <see cref="ObserveCurrentPlayer"/>'s
    /// precondition style.
    /// </summary>
    public GameSnapshot Snapshot()
    {
        if (State is null)
        {
            throw new InvalidOperationException("GameSessionService.Snapshot was called before Start.");
        }

        return new GameSnapshot(State, Players.Values.ToList());
    }

    /// <summary>
    /// Restores a previously-built <see cref="GameSnapshot"/> directly,
    /// bypassing <see cref="GameSetup.Create"/> entirely — used by
    /// <see cref="ResumeAsync"/> and by the anonymous pending-save
    /// rehydration flow (design D2). Raises <see cref="Changed"/> like
    /// <see cref="Start"/>/<see cref="Execute"/> do on success.
    /// </summary>
    public void LoadFrom(GameSnapshot snapshot)
    {
        State = snapshot.State;
        Players = snapshot.Players.ToDictionary(p => p.Id);
        LastEvents = [];
        Changed?.Invoke();
    }

    /// <summary>
    /// Manual save (spec's "save is a manual, explicit action" — never
    /// called automatically from <see cref="Execute"/>). Resolves the
    /// signed-in user id from <paramref name="authStateProvider"/>'s
    /// server-side principal; returns <see cref="SaveOutcome.NotAuthenticated"/>
    /// without touching <see cref="store"/> at all if nobody is signed in.
    /// </summary>
    public async Task<SaveOutcome> SaveAsync(CancellationToken ct = default)
    {
        var userId = await CurrentUserIdAsync();
        if (userId is null)
        {
            return SaveOutcome.NotAuthenticated;
        }

        var overwritten = await store.SaveAsync(userId, Snapshot(), ct);
        OwnerUserId = userId;
        return overwritten ? SaveOutcome.Overwritten : SaveOutcome.Created;
    }

    /// <summary>
    /// Loads the signed-in user's saved game, if any, via <see cref="LoadFrom"/>.
    /// Returns <see langword="false"/> (spec's "resume with no saved game")
    /// without mutating <see cref="State"/> if nobody is signed in or no
    /// save exists.
    /// </summary>
    public async Task<bool> ResumeAsync(CancellationToken ct = default)
    {
        var userId = await CurrentUserIdAsync();
        if (userId is null)
        {
            return false;
        }

        var snapshot = await store.LoadAsync(userId, ct);
        if (snapshot is null)
        {
            return false;
        }

        LoadFrom(snapshot);
        OwnerUserId = userId;
        return true;
    }

    /// <summary>
    /// Spec's "winning deletes the saved row": idempotent no-op unless the
    /// game just reached <see cref="GameStatus.Won"/> AND
    /// <see cref="HasPersistedSave"/> — never calls <see cref="store"/> in
    /// any other case (including anonymous play, where <see cref="OwnerUserId"/>
    /// is always <see langword="null"/>).
    /// </summary>
    public async Task DeleteSaveIfWonAsync(CancellationToken ct = default)
    {
        if (State?.Status is not GameStatus.Won || OwnerUserId is null)
        {
            return;
        }

        await store.DeleteAsync(OwnerUserId, ct);
    }

    private async Task<string?> CurrentUserIdAsync()
    {
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        return authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
