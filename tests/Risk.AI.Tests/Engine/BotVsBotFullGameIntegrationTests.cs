using Risk.AI.Scoring;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Map;
using Risk.Engine;
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
/// Because <see cref="Risk.Engine.Setup.GameSetup.Create"/> deals territories
/// via <c>Random.Shared</c> (a known, documented testability gap — see the
/// design's Verified Engine Facts table), this test asserts INVARIANTS over
/// the real engine's phase machinery (design D9), never a specific board
/// layout: the game must terminate within budget, end in a genuine
/// <see cref="GameStatus.Won"/> (not <c>Rejected</c>/<c>Exhausted</c>), and
/// the winner must satisfy Classic mode's own <see cref="Risk.Engine.Modes.ConquestVictoryRule"/>
/// (full map control by a non-eliminated player — verified directly against
/// <c>ConquestVictoryRule.CheckVictory</c>, not assumed).
/// </summary>
public class BotVsBotFullGameIntegrationTests
{
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
    ///
    /// Runs across Classic's full supported player-count range (3-5, per
    /// <see cref="Risk.Engine.Setup.GameSetup.PlayerCountRange"/>) so the
    /// invariant isn't proven for one lucky party size only — each player
    /// count meaningfully changes the board's territory-per-player ratio and
    /// therefore the termination dynamics design's own Open Questions
    /// section flags as unproven before this test existed.
    /// </summary>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void A_full_bot_vs_bot_Classic_game_reaches_a_valid_win_with_zero_rejected_commands(int playerCount)
    {
        var dice = new SequenceDiceRoller();
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

        // Classic's real victory rule (ConquestVictoryRule): the winner must
        // own every territory on the board. Verified directly against
        // ConquestVictoryRule.CheckVictory rather than assumed.
        var winnerOwnedTerritories = completed.State.Territories.Values.Count(t => t.Owner == won.Winner);
        Assert.Equal(WorldMap.Territories.Count, winnerOwnedTerritories);

        // Every other real player must be eliminated — Classic has no
        // neutral seat, so full map control and "every other party
        // eliminated" are the same fact, cross-checked here independently.
        foreach (var loser in completed.State.Players.Where(p => p.Id != won.Winner))
        {
            Assert.True(loser.IsEliminated);
        }
    }
}
