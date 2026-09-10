using Risk.AI.Scoring;
using Risk.AI.Tests.Fakes;
using Risk.Engine;
using Risk.Engine.Modes;
using Risk.Engine.State;

namespace Risk.AI.Tests.Engine;

/// <summary>
/// The first full-game, bot-vs-bot proof of the entire production
/// <see cref="BotPlayer"/>/<c>Decisions.*</c> stack (Phase 9a, per
/// <c>sdd/risk-ai-bots/design</c>'s D9 and the spec's "All four GameMode
/// bot-vs-bot integration tests are blocking" requirement) — Classic mode
/// specifically. Unlike <c>BotTurnRunnerTests</c> and <c>GameHarnessTests</c>,
/// which fast-forward Claim/Setup via the trivial <see cref="FirstLegalBot"/>
/// fixture and only exercise the REAL <see cref="BotPlayer"/> for a handful
/// of phases/commands, this test drives EVERY seat with the real
/// <see cref="BotPlayer"/> from the very first Claim command all the way to
/// <see cref="GameStatus.Won"/>, via <see cref="BotTurnRunner.RunGame"/>.
///
/// <para>
/// <b>Determinism, verified directly against the engine (correcting an
/// earlier, inaccurate claim in this file's history):</b> for
/// <see cref="GameMode.Classic"/> specifically, <see cref="Risk.Engine.Setup.GameSetup.Create"/>
/// never touches <c>Random.Shared</c> — that only exists in
/// <c>TwoPlayerSetupStrategy</c>/<c>SecretMissionSetupStrategy</c>, neither
/// of which this test exercises. Classic starts every territory unclaimed
/// and deals none of them up front; the only randomness-shaped input is
/// <c>TurnOrder.DetermineFirst</c>'s roll-off, which consumes the
/// constructor-injected, fully deterministic <see cref="SequenceDiceRoller"/>.
/// Territory ownership itself comes entirely from the deterministic Claim
/// phase (<c>ClaimDecision</c>'s scoring, no randomness at all). The
/// practical consequence: a fixed dice sequence reproduces the exact same
/// game, move for move, every run — there is no hidden nondeterminism to
/// average over here. Invariant-style assertions (termination bound, a
/// genuine <see cref="GameStatus.Won"/>, the real victory rule) are used
/// anyway because they are simply the correct shape of assertion for a
/// multi-turn combat simulation — robust to future dice-content or
/// scoring-constant tuning without needing a golden-master board — not
/// because this specific mode's setup is randomized. To actually exercise
/// more than one game trajectory, this test varies the INJECTED DICE
/// SEQUENCE itself across theory cases (see <see cref="ClassicScenarios"/>)
/// — that is the only lever that changes anything in this fully
/// deterministic pipeline.
/// </para>
/// </summary>
public class BotVsBotFullGameIntegrationTests
{
    /// <summary>
    /// Three fixed, cycling dice sequences (all prime length, per
    /// <see cref="SequenceDiceRoller"/>'s own convention, so a cycle never
    /// phases in lockstep with the fixed 1/2/3-attacker/1/2-defender dice
    /// counts) with deliberately different value distributions, so each
    /// drives a genuinely different sequence of battle outcomes — and
    /// therefore a genuinely different game trajectory — rather than
    /// replaying the same recorded game under a different label.
    /// </summary>
    private static readonly IReadOnlyList<int>[] DiceSequenceVariants =
    [
        [6, 5, 4, 3, 2, 1, 6, 4, 2, 5, 3, 1, 6, 6, 1, 4, 3], // length 17 (SequenceDiceRoller's own default)
        [1, 3, 5, 2, 4, 6, 1, 6, 2, 5, 3, 4, 6, 1, 5, 2, 4, 3, 6], // length 19, different distribution/order
        [2, 6, 1, 5, 3, 4, 6, 2, 5, 1, 4, 3, 6, 2, 1, 5, 4, 3, 6, 2, 5, 1, 4], // length 23, different again
    ];

    /// <summary>
    /// Cross product of Classic's full supported player-count range (3-5,
    /// per <see cref="Risk.Engine.Setup.GameSetup.PlayerCountRange"/>) and
    /// the three dice-sequence variants above: 9 cases total, each a
    /// genuinely distinct game trajectory (different party size AND
    /// different combat outcomes), not a cosmetic duplication of one
    /// recorded run.
    /// </summary>
    public static IEnumerable<object[]> ClassicScenarios()
    {
        foreach (var playerCount in new[] { 3, 4, 5 })
        {
            for (var variant = 0; variant < DiceSequenceVariants.Length; variant++)
            {
                yield return [playerCount, variant];
            }
        }
    }

    /// <summary>
    /// Reaching <see cref="BotRunResult.Completed"/> (rather than
    /// <see cref="BotRunResult.Rejected"/>) is itself the proof of the
    /// spec's "Zero-Rejected invariant across full games": <see cref="BotTurnRunner"/>'s
    /// <c>Drive</c> loop (design D5) has exactly three exits —
    /// <c>Completed</c> when <see cref="GameStatus.Won"/> is reached,
    /// <c>Rejected</c> the very first time <see cref="IGameEngine.Execute"/>
    /// returns a rejection (terminal, no retry, no fallback), or
    /// <c>Exhausted</c> when the command budget runs out first. There is no
    /// code path that reaches <c>Completed</c> after having silently
    /// swallowed a <c>Rejected</c> along the way, so asserting the RESULT
    /// TYPE is <c>Completed</c> is sufficient to prove zero <c>Rejected</c>
    /// results occurred anywhere in the run — no separate counter is needed
    /// or possible to observe from outside the runner.
    /// </summary>
    [Theory]
    [MemberData(nameof(ClassicScenarios))]
    public void A_full_bot_vs_bot_Classic_game_reaches_a_valid_win_with_zero_rejected_commands(int playerCount, int diceVariant)
    {
        var dice = new SequenceDiceRoller(DiceSequenceVariants[diceVariant]);
        var harness = GameHarness.Start(GameMode.Classic, playerCount, dice);
        IReadOnlyList<IBotPlayer> bots = harness.State.Players
            .Where(p => !p.IsNeutral)
            .Select(p => (IBotPlayer)new BotPlayer(p.Id))
            .ToArray();
        var runner = new BotTurnRunner(harness.Engine);

        var result = runner.RunGame(harness.State, bots);

        var completed = Assert.IsType<BotRunResult.Completed>(result);
        Assert.True(completed.CommandsIssued < BotWeights.MaxCommandsPerGame,
            $"Game reached the {BotWeights.MaxCommandsPerGame}-command budget without a Won state — treat as a stalemate, not a pass.");

        var won = Assert.IsType<GameStatus.Won>(completed.State.Status);
        var winner = completed.State.Players.Single(p => p.Id == won.Winner);
        Assert.False(winner.IsEliminated);

        // Classic's real victory rule: call ConquestVictoryRule.CheckVictory
        // directly (not a reimplementation of its "owns every territory"
        // logic) so this assertion can never silently drift from the rule
        // it claims to verify.
        Assert.Equal(won.Winner, new ConquestVictoryRule().CheckVictory(completed.State));

        // Every other real player must be eliminated — Classic has no
        // neutral seat, so full map control and "every other party
        // eliminated" are the same fact, cross-checked here independently.
        foreach (var loser in completed.State.Players.Where(p => p.Id != won.Winner))
        {
            Assert.True(loser.IsEliminated);
        }
    }
}
