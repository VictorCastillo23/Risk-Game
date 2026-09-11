using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Risk.AI;
using Risk.AI.Scoring;
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

    /// <summary>
    /// <see cref="IGameStore.SaveAsync"/> threw (DB down/timeout/pool
    /// exhaustion, etc.) — PR5 fix pass, BLOCKER finding. The in-progress,
    /// unsaved game is left completely untouched: <see cref="GameSessionService.State"/>
    /// and <see cref="GameSessionService.OwnerUserId"/> keep whatever value
    /// they held immediately before the failed save attempt.
    /// </summary>
    Failed,
}

/// <summary>
/// Which of the four outcomes <see cref="GameSessionService.ResumeAsync"/>
/// resolved to. Mirrors <see cref="SaveOutcome"/>'s shape: no signed-in user,
/// a signed-in user with nothing saved, an unhandled store failure, or a
/// successful resume. Introduced alongside <see cref="SaveOutcome.Failed"/>
/// (PR5 fix pass, BLOCKER finding) so a caller gets a distinct signal for
/// "the store errored" instead of that being indistinguishable from "there
/// was nothing to resume" — no UI consumes this yet (PR6), so this is the
/// right time to give it its own shape rather than reusing a bare
/// <see langword="bool"/>.
/// </summary>
public enum ResumeOutcome
{
    NotAuthenticated,
    NoSavedGame,
    Failed,
    Resumed,
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
    AuthenticationStateProvider authStateProvider,
    BotTurnRunner botRunner)
{
    public GameState? State { get; private set; }

    public IReadOnlyDictionary<PlayerId, PlayerConfig> Players { get; private set; } =
        new Dictionary<PlayerId, PlayerConfig>();

    public IReadOnlyList<GameEvent> LastEvents { get; private set; } = [];

    public bool IsStarted => State is not null;

    public event Action? Changed;

    /// <summary>
    /// Set by <see cref="AdvanceAiTurns"/> when an AI seat's turn could not
    /// be resolved to completion — either the engine rejected a command the
    /// bot issued, or the per-drain command budget ran out first (design
    /// D1/D2). <see langword="null"/> at the start of every mutator
    /// (<see cref="Start"/>, <see cref="Execute"/>, <see cref="LoadFrom"/>)
    /// before <see cref="AdvanceAiTurns"/> runs, and cleared by
    /// <see cref="Reset"/>. Once set, it is never automatically retried —
    /// surfacing an AI defect is the whole point (design D2).
    /// </summary>
    public AiTurnFailure? AiFailure { get; private set; }

    /// <summary>
    /// The bot instance for each AI-controlled seat, rebuilt whenever seat
    /// control can change (<see cref="Start"/>/<see cref="LoadFrom"/>) via
    /// <see cref="RebuildBotRegistry"/>. Never contains the synthesized
    /// <see cref="GameMode.TwoPlayer"/> neutral seat (always <c>IsAi: false</c>
    /// per <see cref="Start"/>'s own synthesis).
    /// </summary>
    private readonly Dictionary<PlayerId, IBotPlayer> _bots = new();

    /// <summary>
    /// Each registered bot's own causally-derived memory, threaded across
    /// <see cref="AdvanceAiTurns"/> calls for as long as the seat keeps its
    /// registry entry. Reset to <see cref="BotMemory.Empty"/> whenever
    /// <see cref="RebuildBotRegistry"/> runs (design D4: a resumed AI seat
    /// via <see cref="LoadFrom"/> is always starting a fresh turn, so
    /// <c>Empty</c> is correct by construction — see that method's own
    /// remarks).
    /// </summary>
    private readonly Dictionary<PlayerId, BotMemory> _botMemories = new();

    /// <summary>
    /// Rebuilds <see cref="_bots"/>/<see cref="_botMemories"/> from the
    /// current <see cref="Players"/> (spec's "Bot registry construction"):
    /// every <see cref="PlayerConfig.IsAi"/> seat gets a fresh
    /// <see cref="BotPlayer"/> and <see cref="BotMemory.Empty"/>. Called from
    /// <see cref="Start"/>/<see cref="LoadFrom"/> only — seat control is
    /// fixed at game start (spec's "Seat control fixed at game start"
    /// non-goal), so <see cref="Execute"/> never needs to rebuild.
    /// </summary>
    private void RebuildBotRegistry()
    {
        _bots.Clear();
        _botMemories.Clear();

        foreach (var config in Players.Values.Where(c => c.IsAi))
        {
            _bots[config.Id] = new BotPlayer(config.Id);
            _botMemories[config.Id] = BotMemory.Empty;
        }
    }

    /// <summary>
    /// Drains every consecutive AI-controlled seat's turn, starting from
    /// whichever seat is <see cref="State"/>'s current <c>Turn.CurrentPlayer</c>,
    /// until either a human (or the <see cref="GameMode.TwoPlayer"/> neutral,
    /// which never becomes <c>CurrentPlayer</c>) seat is reached,
    /// <see cref="GameStatus.Won"/> is reached, or the outer per-drain
    /// command budget (<see cref="BotWeights.MaxCommandsPerGame"/>, design
    /// D3) is exhausted. Called from <see cref="Start"/>, <see cref="Execute"/>
    /// (on <c>Ok</c>), and <see cref="LoadFrom"/>, always immediately before
    /// their own <see cref="Changed"/> invocation, so
    /// <c>Turn.CurrentPlayer</c> is guaranteed human-or-game-over by the time
    /// any component renders.
    ///
    /// The budget is counted PER CALL (per drain), not per game (design D3):
    /// an all-AI game resolved entirely from <see cref="Start"/> is one
    /// drain and correctly gets the full per-game bound, while a later
    /// <see cref="Execute"/> call's own drain starts its count back at zero —
    /// <see cref="BotTurnRunner.RunTurn"/> already bounds any single seat's
    /// turn on its own via <see cref="BotWeights.MaxCommandsPerTurn"/>, so
    /// this outer counter exists only to bound a CHAIN of seats within one
    /// call.
    ///
    /// On <see cref="BotRunResult.Rejected"/>, <see cref="State"/> is
    /// deliberately NOT reassigned: the engine leaves state unchanged on a
    /// rejection, so <see cref="State"/> already equals
    /// <c>rejected.State</c> — reassigning it would be a no-op at best and
    /// an easy-to-regress footgun at worst if that invariant ever
    /// changed. Never retried, never masked — a bot's illegal command is a
    /// defect (design D2), the same hard-fail posture <see cref="BotTurnRunner"/>
    /// itself has one layer down.
    ///
    /// <para>
    /// <b>Post-RED fix:</b> each loop iteration calls <see cref="MarkSeenByAllBots"/>
    /// on <c>bot.Id</c> before <see cref="BotTurnRunner.RunTurn"/> — required
    /// because <c>RunTurn</c> builds its OWN single-entry <c>{ [bot.Id] = memory }</c>
    /// dictionary internally (it only ever drives one seat's turn), so
    /// <c>BotTurnStep.Advance</c>'s "every tracked bot observes the current
    /// actor" step (design D8, upstream in <c>Risk.AI</c>) can only ever mark
    /// a bot as having seen ITSELF when driven this way — unlike
    /// <see cref="BotTurnRunner.RunGame"/>, which keeps every registered
    /// bot's memory in ONE shared dictionary for the whole game. Without this
    /// backfill, a <see cref="GameMode.TwoPlayer"/> bot's <c>BotMemory.SeenActors</c>
    /// would never reach the 2-actor count <c>Decisions.SetupDecision.IsTwoPlayerPhaseB</c>
    /// requires, so it would never switch to <c>PlaceNeutralTroopsCommand</c>
    /// once its own Setup pool drains — instead retrying <c>PlaceTroopsCommand</c>
    /// with <c>TroopsRemaining == 0</c>, a genuine <c>Rejected</c> the engine
    /// would (and, before this fix, did) return. This was caught by this
    /// batch's own all-AI-game RED run, not called out in the source design.
    /// </para>
    /// </summary>
    private void AdvanceAiTurns()
    {
        var totalCommandsIssued = 0;

        while (State is { Status: GameStatus.InProgress } && _bots.TryGetValue(State.Turn.CurrentPlayer, out var bot))
        {
            if (totalCommandsIssued >= BotWeights.MaxCommandsPerGame)
            {
                AiFailure = AiTurnFailure.BudgetExhausted(bot.Id);
                return;
            }

            MarkSeenByAllBots(bot.Id);

            switch (botRunner.RunTurn(State, bot, _botMemories[bot.Id]))
            {
                case BotRunResult.Completed completed:
                    State = completed.State;
                    _botMemories[bot.Id] = completed.Memories[bot.Id];
                    totalCommandsIssued += completed.CommandsIssued;
                    break;

                case BotRunResult.Rejected rejected:
                    // rejected.State == State (the engine leaves state
                    // unchanged on Rejected) — deliberately not reassigned,
                    // never retried. See this method's own doc comment.
                    AiFailure = AiTurnFailure.Rejected(bot.Id, rejected.Command, rejected.Error);
                    return;

                case BotRunResult.Exhausted exhausted:
                    State = exhausted.State;
                    foreach (var (pid, mem) in exhausted.Memories)
                    {
                        _botMemories[pid] = mem;
                    }

                    AiFailure = AiTurnFailure.BudgetExhausted(bot.Id);
                    return;
            }
        }
    }

    /// <summary>
    /// Records <paramref name="actor"/> as seen (<see cref="BotMemory.WithSeenActor"/>)
    /// in every currently-registered bot's memory — the cross-seat backfill
    /// <see cref="AdvanceAiTurns"/>'s per-seat <see cref="BotTurnRunner.RunTurn"/>
    /// calls need (see that method's own doc comment) and, via <see cref="Execute"/>'s
    /// own call site below, the same backfill for a HUMAN seat's turn, which
    /// never otherwise passes through <see cref="AdvanceAiTurns"/> at all. A
    /// no-op for any bot that has already recorded <paramref name="actor"/>
    /// (<see cref="BotMemory.WithSeenActor"/> itself is idempotent).
    /// </summary>
    private void MarkSeenByAllBots(PlayerId actor)
    {
        foreach (var id in _bots.Keys)
        {
            _botMemories[id] = _botMemories[id].WithSeenActor(actor);
        }
    }

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

            AiFailure = null;
            RebuildBotRegistry();
            AdvanceAiTurns();

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

            // Backfill for a human seat's own turn (see MarkSeenByAllBots'
            // doc comment) — command.Actor is guaranteed to equal the
            // pre-command Turn.CurrentPlayer here, per the engine's own
            // actor-is-current-player validation gate.
            MarkSeenByAllBots(command.Actor);

            AiFailure = null;
            AdvanceAiTurns();

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

        // Design D7: a reset session must not retain seat->bot bindings, nor
        // a stale failure, from whatever game was previously loaded here.
        AiFailure = null;
        _bots.Clear();
        _botMemories.Clear();

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

        AiFailure = null;
        RebuildBotRegistry();
        AdvanceAiTurns();

        Changed?.Invoke();
    }

    /// <summary>
    /// Manual save (spec's "save is a manual, explicit action" — never
    /// called automatically from <see cref="Execute"/>). Resolves the
    /// signed-in user id from <paramref name="authStateProvider"/>'s
    /// server-side principal; returns <see cref="SaveOutcome.NotAuthenticated"/>
    /// without touching <see cref="store"/> at all if nobody is signed in.
    ///
    /// BLOCKER fix (PR5 fresh-context review): <see cref="store"/>'s call is
    /// wrapped in a try/catch. An unhandled exception from a Blazor Server
    /// event handler faults the entire circuit, which would destroy the
    /// caller's in-progress, unsaved game outright — strictly worse than
    /// just failing to save it. On failure this returns
    /// <see cref="SaveOutcome.Failed"/> and leaves <see cref="State"/>/
    /// <see cref="OwnerUserId"/> exactly as they were (the assignment to
    /// <see cref="OwnerUserId"/> below only ever runs after the store call
    /// has already succeeded). Catches <see cref="Exception"/> broadly
    /// rather than a store-specific type like <c>DbUpdateException</c>: this
    /// service only knows <see cref="IGameStore"/>'s abstract contract (design
    /// D4), never that today's implementation happens to be EF Core, so the
    /// failure boundary has to be equally opaque. <see cref="OperationCanceledException"/>
    /// (e.g. the caller's own <paramref name="ct"/> firing) is deliberately
    /// NOT treated as a save failure — it is allowed to propagate as
    /// cancellation, not be reported as an infra error.
    /// </summary>
    public async Task<SaveOutcome> SaveAsync(CancellationToken ct = default)
    {
        var userId = await CurrentUserIdAsync();
        if (userId is null)
        {
            return SaveOutcome.NotAuthenticated;
        }

        bool overwritten;
        try
        {
            overwritten = await store.SaveAsync(userId, Snapshot(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SaveOutcome.Failed;
        }

        OwnerUserId = userId;
        return overwritten ? SaveOutcome.Overwritten : SaveOutcome.Created;
    }

    /// <summary>
    /// Loads the signed-in user's saved game, if any, via <see cref="LoadFrom"/>.
    /// Returns <see cref="ResumeOutcome.NoSavedGame"/> (spec's "resume with
    /// no saved game") without mutating <see cref="State"/> if nobody is
    /// signed in or no save exists (this also covers an incompatible/stale
    /// schema version — <see cref="IGameStore.LoadAsync"/> reports that the
    /// same way, per CRITICAL #2 of the PR5 fresh-context review).
    ///
    /// BLOCKER fix (PR5 fresh-context review): same try/catch rationale as
    /// <see cref="SaveAsync"/> — an unhandled store exception here would
    /// otherwise fault the circuit instead of leaving the caller with a
    /// clean "resume didn't work" signal.
    /// </summary>
    public async Task<ResumeOutcome> ResumeAsync(CancellationToken ct = default)
    {
        var userId = await CurrentUserIdAsync();
        if (userId is null)
        {
            return ResumeOutcome.NotAuthenticated;
        }

        GameSnapshot? snapshot;
        try
        {
            snapshot = await store.LoadAsync(userId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ResumeOutcome.Failed;
        }

        if (snapshot is null)
        {
            return ResumeOutcome.NoSavedGame;
        }

        LoadFrom(snapshot);
        OwnerUserId = userId;
        return ResumeOutcome.Resumed;
    }

    /// <summary>
    /// Spec's "winning deletes the saved row": idempotent no-op unless the
    /// game just reached <see cref="GameStatus.Won"/> AND
    /// <see cref="HasPersistedSave"/> — never calls <see cref="store"/> in
    /// any other case (including anonymous play, where <see cref="OwnerUserId"/>
    /// is always <see langword="null"/>).
    ///
    /// CRITICAL #1 fix (PR5 fresh-context review): this is cosmetic
    /// housekeeping (deleting a now-stale save row after a win) riding on
    /// the most important terminal-state UI a player sees. It must never be
    /// able to disrupt that experience, so any failure from <see cref="store"/>
    /// is deliberately swallowed — this method's contract is "best-effort,
    /// never throws", by design. Nothing currently consumes a return value,
    /// so there is nothing to signal back; a future caller that needs to
    /// know can be given one without changing this method's core promise.
    /// </summary>
    public async Task DeleteSaveIfWonAsync(CancellationToken ct = default)
    {
        if (State?.Status is not GameStatus.Won || OwnerUserId is null)
        {
            return;
        }

        try
        {
            await store.DeleteAsync(OwnerUserId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort by design — see the doc comment above. Swallowed
            // deliberately, not a bug: the victory screen must render either
            // way.
        }
    }

    /// <summary>
    /// Task 6.2's confirm-before-overwrite pre-check: asks the store
    /// directly for the signed-in user's saved row WITHOUT touching this
    /// session's own state, unlike <see cref="HasPersistedSave"/> (which
    /// only reflects a save/resume this exact session already performed).
    /// A signed-in user who never saved from this particular circuit still
    /// gets an accurate "you already have a save" signal — needed so
    /// <c>SavePanel</c> can show its confirm-before-overwrite prompt BEFORE
    /// calling <see cref="SaveAsync"/>, not after.
    ///
    /// Same failure-safe contract as <see cref="SaveAsync"/>/<see cref="ResumeAsync"/>:
    /// returns <see langword="null"/> (same as "no save exists") on any
    /// <see cref="store"/> failure rather than throwing — a confirm-before-overwrite
    /// pre-check must never be able to crash the save button.
    /// </summary>
    public async Task<SavedGameSummary?> GetSaveSummaryAsync(CancellationToken ct = default)
    {
        var userId = await CurrentUserIdAsync();
        if (userId is null)
        {
            return null;
        }

        try
        {
            return await store.GetSummaryAsync(userId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Task 6.4's anonymous-save-then-login completion, as one seam on this
    /// service (its own "single stateful seam" convention) rather than
    /// duplicated Razor code-behind logic: composes <see cref="LoadFrom"/>
    /// then <see cref="SaveAsync"/> — <c>Game.razor</c>'s pending-save
    /// rehydration calls this once, instead of the same two-step sequence
    /// spread across component code.
    ///
    /// Deliberately loads <paramref name="snapshot"/> even if the
    /// subsequent save fails: the caller just authenticated specifically to
    /// finish saving a game they were actively playing seconds earlier
    /// (design D2's stash-then-redirect flow) — silently discarding that
    /// game locally because of an infra hiccup on the save would be
    /// strictly worse than a failed save alone (mirrors <see cref="SaveAsync"/>'s
    /// own "still playable" guarantee on failure).
    ///
    /// CRITICAL fix (PR6 fresh-context review): also cleans up a just-saved
    /// row when <paramref name="snapshot"/> is already <see cref="GameStatus.Won"/>
    /// (e.g. an anonymous player won, then clicked "Guardar partida" and
    /// completed the login redirect). This can't be left to <c>Game.razor</c>'s
    /// <c>OnSessionChanged</c>/<see cref="DeleteSaveIfWonAsync"/> hookup the
    /// way a mid-session win is: <see cref="LoadFrom"/> raises <see cref="Changed"/>
    /// synchronously, before the caller has had a chance to subscribe to it
    /// for this specific rehydration, so that event can't be relied on here.
    /// Calling <see cref="DeleteSaveIfWonAsync"/> directly, after <see cref="SaveAsync"/>
    /// has had a chance to set <see cref="OwnerUserId"/>, makes this
    /// self-contained and independent of any caller's subscription timing.
    /// </summary>
    public async Task<SaveOutcome> RehydrateAndSaveAsync(GameSnapshot snapshot, CancellationToken ct = default)
    {
        LoadFrom(snapshot);
        var outcome = await SaveAsync(ct);

        if (State?.Status is GameStatus.Won)
        {
            await DeleteSaveIfWonAsync(ct);
        }

        return outcome;
    }

    private async Task<string?> CurrentUserIdAsync()
    {
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        return authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
