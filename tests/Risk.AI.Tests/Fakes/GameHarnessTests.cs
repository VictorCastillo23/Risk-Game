using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.State;

namespace Risk.AI.Tests.Fakes;

/// <summary>
/// Self-tests <see cref="GameHarness"/> (task 8.1): every fast-forward helper
/// actually lands the real engine in the phase it promises, for every mode
/// that helper claims to support, and <see cref="GameHarness.Apply"/> throws
/// rather than silently swallowing a <c>Rejected</c> result — the same
/// "never mask a rejection" discipline the production
/// <see cref="Risk.AI.BotTurnRunner"/> is held to (spec's hard-fail
/// requirement), just enforced here via an exception instead of a
/// <c>BotRunResult</c> variant, since this is test-only plumbing.
/// </summary>
public class GameHarnessTests
{
    [Fact]
    public void Start_Classic_lands_in_the_claim_phase_with_every_territory_unclaimed()
    {
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller());

        Assert.Equal(TurnPhase.Claim, harness.State.Turn.Phase);
        Assert.All(harness.State.Territories.Values, t => Assert.Null(t.Owner));
    }

    [Fact]
    public void Start_SecretMission_lands_directly_in_the_setup_phase_with_no_claim_phase()
    {
        var harness = GameHarness.Start(GameMode.SecretMission, 3, new SequenceDiceRoller());

        Assert.Equal(TurnPhase.Setup, harness.State.Turn.Phase);
    }

    [Fact]
    public void FastForwardToSetup_completes_the_claim_phase_for_Classic()
    {
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller())
            .FastForwardToSetup();

        Assert.Equal(TurnPhase.Setup, harness.State.Turn.Phase);
        Assert.All(harness.State.Territories.Values, t => Assert.NotNull(t.Owner));
    }

    [Fact]
    public void FastForwardToSetup_is_a_no_op_when_the_mode_has_no_claim_phase()
    {
        var harness = GameHarness.Start(GameMode.SecretMission, 3, new SequenceDiceRoller())
            .FastForwardToSetup();

        Assert.Equal(TurnPhase.Setup, harness.State.Turn.Phase);
    }

    [Fact]
    public void FastForwardToSelectHeadquarters_completes_claim_and_setup_for_Capital()
    {
        var harness = GameHarness.Start(GameMode.Capital, 3, new SequenceDiceRoller())
            .FastForwardToSelectHeadquarters();

        Assert.Equal(TurnPhase.SelectHeadquarters, harness.State.Turn.Phase);
    }

    [Fact]
    public void FastForwardToSetup_throws_a_clear_distinguishable_exception_when_the_iteration_cap_is_exceeded()
    {
        // Classic's Claim phase needs 42 ClaimTerritoryCommands to complete;
        // a cap of 2 proves the safety valve fires well before any
        // legitimate fast-forward could ever need it. (A genuinely
        // non-terminating LEGAL command sequence isn't reachable through any
        // of Claim/Setup/SelectHeadquarters's current commands — each one
        // consumes a strictly-decreasing resource: remaining unclaimed
        // territories, a player's own troop pool, or a one-shot headquarters
        // pick, verified directly against GameEngine's own validation for
        // each — so this test proves the MECHANISM via a deliberately tiny
        // cap rather than via a contrived infinite-loop stub bot.)
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller(), maxFastForwardIterations: 2);

        var exception = Assert.Throws<InvalidOperationException>(() => { harness.FastForwardToSetup(); });

        Assert.Contains("exceeded", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", exception.Message);
        Assert.DoesNotContain("rejected", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(GameMode.Classic, 3)]
    [InlineData(GameMode.SecretMission, 3)]
    [InlineData(GameMode.Capital, 3)]
    public void FastForwardToFirstReinforce_reaches_the_reinforce_phase(GameMode mode, int playerCount)
    {
        var harness = GameHarness.Start(mode, playerCount, new SequenceDiceRoller())
            .FastForwardToFirstReinforce();

        Assert.Equal(TurnPhase.Reinforce, harness.State.Turn.Phase);
    }

    [Fact]
    public void FastForwardToFirstReinforce_reaches_the_reinforce_phase_for_TwoPlayer_after_draining_the_neutral_pool()
    {
        // TwoPlayer's Setup Phase B (design D8) requires both real players to
        // fully drain their own 26-troop pool before either can place on the
        // neutral's territories; this exercises that whole sequence via a
        // real engine, not a fixture shortcut.
        var harness = GameHarness.Start(GameMode.TwoPlayer, 2, new SequenceDiceRoller())
            .FastForwardToFirstReinforce();

        Assert.Equal(TurnPhase.Reinforce, harness.State.Turn.Phase);

        var neutral = harness.State.Players.Single(p => p.IsNeutral);
        Assert.Equal(0, neutral.TroopsRemaining);

        // Only the newly-arrived Reinforce player's pool is seeded at the
        // Setup->Reinforce transition; the other human's pool stays at the
        // 0 it reached when their own Setup placements (Phase A) drained.
        var waitingPlayer = harness.State.Players.Single(p => !p.IsNeutral && p.Id != harness.State.Turn.CurrentPlayer);
        Assert.Equal(0, waitingPlayer.TroopsRemaining);
    }

    [Fact]
    public void Apply_advances_state_when_the_command_is_legal()
    {
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller());
        var actor = harness.State.Turn.CurrentPlayer;
        var territory = harness.State.Territories.First(kv => kv.Value.Owner is null).Key;

        harness.Apply(new ClaimTerritoryCommand(actor, territory, 1));

        Assert.Equal(actor, harness.State.Territories[territory].Owner);
    }

    [Fact]
    public void Apply_throws_and_leaves_the_error_traceable_when_the_command_is_rejected()
    {
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller());
        var wrongActor = new PlayerId(999);

        var exception = Assert.Throws<InvalidOperationException>(
            () => { harness.Apply(new EndPhaseCommand(wrongActor)); });

        Assert.Contains("rejected", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ViewFor_returns_a_redacted_view_reflecting_current_state()
    {
        var harness = GameHarness.Start(GameMode.Classic, 3, new SequenceDiceRoller());

        var view = harness.ViewFor(harness.State.Turn.CurrentPlayer);

        Assert.Equal(harness.State.Turn, view.Turn);
    }

    [Fact]
    public void WithMissions_overrides_only_the_specified_seats_missions()
    {
        var harness = GameHarness.Start(GameMode.SecretMission, 3, new SequenceDiceRoller());
        var forced = new OccupyTerritories(18, MinArmiesPerTerritory: 2);
        var untouchedBefore = harness.ViewFor(new PlayerId(1)).OwnEffectiveMission;

        harness.WithMissions(new Dictionary<PlayerId, MissionCard> { [new PlayerId(0)] = forced });

        Assert.Equal(forced, harness.ViewFor(new PlayerId(0)).OwnEffectiveMission);
        Assert.Equal(untouchedBefore, harness.ViewFor(new PlayerId(1)).OwnEffectiveMission);
    }
}
