using Risk.AI;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Web.Models;
using Risk.Web.Persistence;
using Risk.Web.Services;
using Risk.Web.Tests.Fakes;
using Risk.Web.Tests.Persistence;

namespace Risk.Web.Tests.Services;

/// <summary>
/// Task 5.6: proves the session layer (<see cref="GameSessionService"/>) and
/// PR3/4's serializer (<see cref="GameSnapshotSerializer"/>, exercised here
/// only indirectly through <see cref="FakeGameStore"/>'s real round-trip —
/// see that fake's own doc comment) genuinely compose. Plays a real game
/// partway through <see cref="GameSessionService.Execute"/> (same
/// dispatch-only style as <see cref="GameSessionServiceFullGameIntegrationTests"/>),
/// saves, <see cref="GameSessionService.Reset"/>s the session (simulating a
/// new circuit/browser tab — a fresh <see cref="GameSessionService"/>
/// instance, same as a new Blazor Server circuit would construct via DI),
/// resumes, and asserts the resumed state is structurally equivalent to what
/// was saved — never <c>.Equals</c>/<c>==</c> on <see cref="GameState"/>
/// itself (this codebase's documented reference-equality gotcha, per
/// <c>Persistence/GameStateAssertions.cs</c>). Reuses
/// <see cref="GameStateAssertions.AssertStructurallyEqual"/> rather than a
/// local reimplementation (readability fix, PR5 fix pass): <see langword="internal"/>
/// in C# is assembly-scoped, not namespace-scoped, and this test class lives
/// in the same <c>Risk.Web.Tests</c> assembly, so there was never a
/// visibility reason to duplicate it — the duplicate was also strictly less
/// thorough (missing <c>IsNeutral</c>/<c>HeadquartersId</c>/<c>Mission</c>/
/// turn-flags/<c>PendingOccupation</c>/<c>Won</c> details).
/// </summary>
public class GameSessionServiceSaveResumeIntegrationTests
{
    private const string UserId = "resume-user";

    [Fact]
    public async Task SaveThenResumeAfterReset_ReproducesEquivalentGameState()
    {
        var store = new FakeGameStore();
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var session = new GameSessionService(engine, new AlwaysAttackerWinsDiceRoller(), store, StubAuthenticationStateProvider.SignedIn(UserId), new BotTurnRunner(engine));

        var rows = new List<PlayerSetupRow>
        {
            new("Ana", "#E53935", false),
            new("Beto", "#1E88E5", false)
        };
        var startResult = session.Start(rows, GameMode.TwoPlayer);
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(startResult);

        var attacker = session.State!.Turn.CurrentPlayer;

        // Drive Setup, then a few Reinforce/Attack rounds — enough to
        // produce non-trivial state (uneven troop counts, at least one
        // BattleResolved/TerritoryConquered in the log) before saving.
        while (session.State!.Turn.Phase == TurnPhase.Setup)
        {
            PlaceOneStartingTroop(session);
        }

        for (var round = 0; round < 3 && session.State!.Status is not GameStatus.Won; round++)
        {
            PlayOneReinforceAttackFortifyRound(session, attacker);
        }

        var stateBeforeSave = session.State!;
        var outcome = await session.SaveAsync();
        Assert.Equal(SaveOutcome.Created, outcome);
        Assert.Equal(1, store.SaveCount);

        // Simulate a new circuit/browser: a brand-new GameSessionService
        // instance (exactly what Blazor Server's scoped DI would construct
        // for a fresh circuit) rather than reusing `session` after Reset —
        // this is the strongest possible proof that resume does not depend
        // on any in-memory state surviving.
        var freshSession = new GameSessionService(engine, new AlwaysAttackerWinsDiceRoller(), store, StubAuthenticationStateProvider.SignedIn(UserId), new BotTurnRunner(engine));
        Assert.False(freshSession.IsStarted);

        var resumed = await freshSession.ResumeAsync();

        Assert.Equal(ResumeOutcome.Resumed, resumed);
        Assert.Equal(UserId, freshSession.OwnerUserId);
        GameStateAssertions.AssertStructurallyEqual(stateBeforeSave, freshSession.State!);
        // TwoPlayer mode's engine-created neutral army (design D2's own
        // PlayerConfig, synthesized in Start) is a 3rd PlayerConfig on top
        // of the 2 human rows.
        Assert.Equal(3, freshSession.Players.Count);
        Assert.Equal("Ana", freshSession.ConfigFor(new PlayerId(0)).Name);
        Assert.Equal("Beto", freshSession.ConfigFor(new PlayerId(1)).Name);
    }

    [Fact]
    public async Task ResetBetweenSaveAndResume_DoesNotPreventResumeOnTheSameSession()
    {
        var store = new FakeGameStore();
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var session = new GameSessionService(engine, new AlwaysAttackerWinsDiceRoller(), store, StubAuthenticationStateProvider.SignedIn(UserId), new BotTurnRunner(engine));

        var rows = new List<PlayerSetupRow>
        {
            new("Ana", "#E53935", false),
            new("Beto", "#1E88E5", false)
        };
        session.Start(rows, GameMode.TwoPlayer);
        while (session.State!.Turn.Phase == TurnPhase.Setup)
        {
            PlaceOneStartingTroop(session);
        }
        var stateBeforeSave = session.State!;
        await session.SaveAsync();

        // Reset() now raises Changed (PR5) — proving that firing it doesn't
        // corrupt the already-persisted row the next ResumeAsync call reads.
        session.Reset();
        Assert.False(session.IsStarted);
        Assert.Null(session.OwnerUserId);

        var resumed = await session.ResumeAsync();

        Assert.Equal(ResumeOutcome.Resumed, resumed);
        GameStateAssertions.AssertStructurallyEqual(stateBeforeSave, session.State!);
    }

    /// <summary>
    /// Task 6.6: exercises the FULL anonymous-save-then-login flow
    /// (design D2) end-to-end at the level just below the actual
    /// <c>ProtectedSessionStorage</c> call (see <see cref="PendingSave"/>'s
    /// own doc comment for why that boundary needs a browser/JS interop and
    /// can't be driven from here): anonymous session plays a bit and takes a
    /// <see cref="GameSessionService.Snapshot"/>, that snapshot is wrapped
    /// into a <see cref="PendingSave"/> and round-tripped through
    /// <see cref="System.Text.Json.JsonSerializer"/> using PLAIN default
    /// options (simulating exactly what <c>ProtectedSessionStorage</c> does
    /// to the stashed value — see <see cref="PendingSaveTests"/> for the
    /// dedicated regression guard on that specific shape), then a brand-new
    /// signed-in <see cref="GameSessionService"/> instance (simulating the
    /// fresh, authenticated circuit the login-redirect lands on) calls
    /// <see cref="GameSessionService.RehydrateAndSaveAsync"/> — the same
    /// composition <c>Game.razor</c>'s <c>OnAfterRenderAsync</c> calls.
    /// </summary>
    [Fact]
    public async Task AnonymousSaveThenLoginRedirectFlow_RehydratesAndPersistsTheSameGame()
    {
        var store = new FakeGameStore();
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var anonymousSession = new GameSessionService(engine, new AlwaysAttackerWinsDiceRoller(), store, StubAuthenticationStateProvider.Anonymous(), new BotTurnRunner(engine));

        var rows = new List<PlayerSetupRow>
        {
            new("Ana", "#E53935", false),
            new("Beto", "#1E88E5", false)
        };
        anonymousSession.Start(rows, GameMode.TwoPlayer);
        while (anonymousSession.State!.Turn.Phase == TurnPhase.Setup)
        {
            PlaceOneStartingTroop(anonymousSession);
        }

        var stateBeforeStash = anonymousSession.State!;

        // "Guardar partida" clicked while anonymous (SavePanel's anonymous
        // branch): stash into what would be ProtectedSessionStorage. Using
        // JsonSerializer directly with NO custom options here is the whole
        // point — that's exactly what ProtectedSessionStorage does, and is
        // precisely the constraint PendingSave exists to satisfy.
        var stashedJson = System.Text.Json.JsonSerializer.Serialize(PendingSave.From(anonymousSession.Snapshot()));

        // forceLoad navigation to /Account/Login tears down anonymousSession's
        // circuit entirely — nothing from it survives except stashedJson.

        // Login succeeds, browser redirects back to /game with a brand-new,
        // authenticated circuit/GameSessionService instance.
        var retrieved = System.Text.Json.JsonSerializer.Deserialize<PendingSave>(stashedJson);
        Assert.NotNull(retrieved);

        var authenticatedSession = new GameSessionService(engine, new AlwaysAttackerWinsDiceRoller(), store, StubAuthenticationStateProvider.SignedIn(UserId), new BotTurnRunner(engine));
        var outcome = await authenticatedSession.RehydrateAndSaveAsync(retrieved!.ToSnapshot());

        Assert.Equal(SaveOutcome.Created, outcome);
        Assert.True(authenticatedSession.IsStarted);
        Assert.Equal(UserId, authenticatedSession.OwnerUserId);
        GameStateAssertions.AssertStructurallyEqual(stateBeforeStash, authenticatedSession.State!);

        // The save actually landed in the store too, not just this
        // session's in-memory State — a second, independent session can
        // resume it.
        var resumerSession = new GameSessionService(engine, new AlwaysAttackerWinsDiceRoller(), store, StubAuthenticationStateProvider.SignedIn(UserId), new BotTurnRunner(engine));
        var resumed = await resumerSession.ResumeAsync();
        Assert.Equal(ResumeOutcome.Resumed, resumed);
        GameStateAssertions.AssertStructurallyEqual(stateBeforeStash, resumerSession.State!);
    }

    private static void PlaceOneStartingTroop(GameSessionService session)
    {
        var state = session.State!;
        var actor = state.Turn.CurrentPlayer;
        var actorPool = state.Players.Single(p => p.Id == actor).TroopsRemaining;

        if (state.Turn.Phase == TurnPhase.Setup && actorPool == 0)
        {
            var neutralId = state.Players.Single(p => p.IsNeutral).Id;
            var neutralTerritory = state.Territories.First(kv => kv.Value.Owner == neutralId).Key;
            var neutralResult = session.Execute(new PlaceNeutralTroopsCommand(actor, neutralTerritory, 1));
            Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(neutralResult);
            return;
        }

        var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
        var result = session.Execute(new PlaceTroopsCommand(actor, territory, 1));
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
    }

    /// <summary>
    /// Plays one full Reinforce -&gt; Attack -&gt; Fortify round for whichever
    /// player is currently active, favoring an actual attack when
    /// <paramref name="attacker"/> has one available so the saved state has
    /// non-trivial territory/troop deltas and at least one <see cref="BattleResolved"/>
    /// event by the time this test saves.
    /// </summary>
    private static void PlayOneReinforceAttackFortifyRound(GameSessionService session, PlayerId attacker)
    {
        for (var phaseGuard = 0; phaseGuard < 3 && session.State!.Status is not GameStatus.Won; phaseGuard++)
        {
            var state = session.State!;
            var actor = state.Turn.CurrentPlayer;

            if (state.Turn.Phase == TurnPhase.Reinforce)
            {
                while (session.State!.Players.Single(p => p.Id == actor).TroopsRemaining > 0)
                {
                    var territory = session.State!.Territories.First(kv => kv.Value.Owner == actor).Key;
                    session.Execute(new PlaceTroopsCommand(actor, territory, 1));
                }

                session.Execute(new EndPhaseCommand(actor));
                continue;
            }

            if (state.Turn.Phase == TurnPhase.Attack)
            {
                if (actor == attacker && TryFindAttack(state, actor, out var from, out var to))
                {
                    var attackResult = session.Execute(new AttackCommand(actor, from, to, 1));
                    if (attackResult is CommandResult<GameState, GameEvent>.Ok && session.State!.Status is not GameStatus.Won)
                    {
                        var pending = session.State!.Turn.PendingOccupation;
                        if (pending is not null)
                        {
                            session.Execute(new OccupyCommand(actor, pending.MinimumTroops));
                        }
                    }

                    return; // one attack is enough non-trivial state for this test
                }

                session.Execute(new EndPhaseCommand(actor));
                continue;
            }

            if (state.Turn.Phase == TurnPhase.Fortify)
            {
                session.Execute(new EndPhaseCommand(actor));
                return;
            }
        }
    }

    private static bool TryFindAttack(GameState state, PlayerId actor, out TerritoryId from, out TerritoryId to)
    {
        foreach (var (territoryId, territoryState) in state.Territories)
        {
            if (territoryState.Owner != actor || territoryState.Troops < 2)
            {
                continue;
            }

            foreach (var neighbor in WorldMap.NeighborsOf(territoryId))
            {
                if (state.Territories[neighbor].Owner != actor)
                {
                    from = territoryId;
                    to = neighbor;
                    return true;
                }
            }
        }

        from = default;
        to = default;
        return false;
    }
}
