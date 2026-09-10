using Risk.AI.Scoring;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Modes;
using Risk.Engine.Rules;
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

    /// <summary>
    /// Cross product of SecretMission's full supported player-count range
    /// (3-5, per <see cref="Risk.Engine.Setup.GameSetup.PlayerCountRange"/>)
    /// and the three dice-sequence variants — mirrors <see cref="ClassicScenarios"/>
    /// exactly. Missions here come from <see cref="Risk.Engine.Modes.SecretMissionSetupStrategy"/>'s
    /// own <c>Random.Shared</c> deal (design D9's random-board invariant
    /// testing), not forced — this is the "does the real deal terminate"
    /// proof; <see cref="SecretMissionForcedArchetypeScenarios"/> below is the
    /// "every archetype genuinely works" proof.
    /// </summary>
    public static IEnumerable<object[]> SecretMissionScenarios()
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
    /// First real end-to-end proof of <see cref="Risk.AI.Scoring.MissionScoring"/>'s
    /// per-archetype weighting (design's bot-objective-awareness capability):
    /// every seat is a real <see cref="BotPlayer"/> pursuing whatever mission
    /// <see cref="Risk.Engine.Modes.SecretMissionSetupStrategy"/> dealt it, and
    /// the game must terminate with SecretMission's own win condition — a
    /// player's <see cref="Risk.Engine.Views.PlayerView.OwnEffectiveMission"/>
    /// being satisfied — not simple map domination. Same zero-<c>Rejected</c>
    /// reasoning as the Classic test above: reaching <c>Completed</c> is
    /// itself sufficient proof of zero <c>Rejected</c> results.
    /// </summary>
    [Theory]
    [MemberData(nameof(SecretMissionScenarios))]
    public void A_full_bot_vs_bot_SecretMission_game_reaches_a_valid_win_with_zero_rejected_commands(int playerCount, int diceVariant)
    {
        var dice = new SequenceDiceRoller(DiceSequenceVariants[diceVariant]);
        var harness = GameHarness.Start(GameMode.SecretMission, playerCount, dice);
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

        // SecretMission's real victory rule, called directly on the final
        // state — mirrors the Classic test's ConquestVictoryRule check, so
        // this assertion can never silently drift from the rule it claims to
        // verify.
        Assert.Equal(won.Winner, new SecretMissionVictoryRule().CheckVictory(completed.State));

        // Independent check, NOT delegating back to SecretMissionVictoryRule:
        // re-derive completion from the winner's OWN redacted OwnEffectiveMission
        // (exactly what a real bot/AI client would see via Observe) against the
        // final board, using a fresh implementation of each archetype's plain
        // win condition (see IsMissionGenuinelySatisfied below). This is the
        // batch's explicit ask: don't just trust GameStatus.Won fired.
        var winnerView = harness.Engine.Observe(completed.State, won.Winner);
        Assert.NotNull(winnerView.OwnEffectiveMission);
        Assert.True(
            IsMissionGenuinelySatisfied(completed.State, won.Winner, winnerView.OwnEffectiveMission!),
            $"Winner {won.Winner} was reported Won, but their OwnEffectiveMission " +
            $"({winnerView.OwnEffectiveMission}) is not actually satisfied by the final board state.");
    }

    /// <summary>
    /// Latin square over SecretMission's three mission archetypes
    /// (<see cref="OccupyTerritories"/>, <see cref="ConquerContinents"/>,
    /// <see cref="EliminateArmy"/>) across a fixed 3-player game: each of the
    /// 3 rotations assigns a DIFFERENT archetype to each seat, so across all
    /// 3 rotations every seat gets every archetype exactly once. Missions are
    /// forced via <see cref="GameHarness.WithMissions"/> — territory dealing
    /// and combat dice stay genuinely random/varied (one dice-sequence
    /// variant per rotation) — because <see cref="Random.Shared"/>-based
    /// mission dealing (see <see cref="SecretMissionScenarios"/> above) gives
    /// no guarantee any single run exercises all three archetypes, and the
    /// batch brief explicitly calls for forcing assignments to get that
    /// guarantee.
    /// </summary>
    public static IEnumerable<object[]> SecretMissionForcedArchetypeScenarios()
    {
        yield return [0];
        yield return [1];
        yield return [2];
    }

    [Theory]
    [MemberData(nameof(SecretMissionForcedArchetypeScenarios))]
    public void A_full_bot_vs_bot_SecretMission_game_reaches_a_valid_win_for_every_forced_mission_archetype(int rotation)
    {
        const int playerCount = 3;
        var dice = new SequenceDiceRoller(DiceSequenceVariants[rotation]);
        var harness = GameHarness.Start(GameMode.SecretMission, playerCount, dice)
            .WithMissions(BuildLatinSquareMissions(rotation));
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
        Assert.Equal(won.Winner, new SecretMissionVictoryRule().CheckVictory(completed.State));

        var winnerView = harness.Engine.Observe(completed.State, won.Winner);
        Assert.NotNull(winnerView.OwnEffectiveMission);
        Assert.True(
            IsMissionGenuinelySatisfied(completed.State, won.Winner, winnerView.OwnEffectiveMission!),
            $"Winner {won.Winner} was reported Won, but their OwnEffectiveMission " +
            $"({winnerView.OwnEffectiveMission}) is not actually satisfied by the final board state.");
    }

    /// <summary>
    /// Rotation N assigns archetype <c>(seat - N) mod 3</c> to each seat (0 =
    /// Occupy, 1 = ConquerContinents, 2 = EliminateArmy), so all 3 rotations
    /// together form a Latin square: every seat gets every archetype exactly
    /// once, and no rotation ever assigns EliminateArmy targeting its own
    /// holder (the target is always the NEXT seat in the cycle).
    /// </summary>
    private static IReadOnlyDictionary<PlayerId, MissionCard> BuildLatinSquareMissions(int rotation)
    {
        var occupy = new OccupyTerritories(18, MinArmiesPerTerritory: 2);
        var continents = new ConquerContinents([new ContinentId("NA"), new ContinentId("OC")]);

        MissionCard ArchetypeFor(int seat)
        {
            var slot = ((seat - rotation) % 3 + 3) % 3;
            return slot switch
            {
                0 => occupy,
                1 => continents,
                _ => new EliminateArmy(new ArmyId((seat + 1) % 3)),
            };
        }

        return Enumerable.Range(0, 3).ToDictionary(seat => new PlayerId(seat), ArchetypeFor);
    }

    /// <summary>
    /// Independently re-derives whether <paramref name="mission"/> (the
    /// winner's <c>OwnEffectiveMission</c>, exactly as reported by
    /// <c>Observe</c>) is genuinely satisfied on <paramref name="state"/>'s
    /// final board — computed from each archetype's plain win condition
    /// directly against public state (<see cref="ContinentControl.IsFullyOwnedBy"/>,
    /// <see cref="Continents.All"/>, territory ownership/troop counts,
    /// elimination status), never by calling <see cref="SecretMissionVictoryRule"/>
    /// or any of its internals.
    /// </summary>
    private static bool IsMissionGenuinelySatisfied(GameState state, PlayerId player, MissionCard mission) =>
        mission switch
        {
            OccupyTerritories(var count, var minArmies) =>
                state.Territories.Values.Count(t => t.Owner == player && t.Troops >= minArmies) >= count,
            ConquerContinents(var required, var wildcardCount) => ConquerContinentsSatisfied(state, player, required, wildcardCount),
            EliminateArmy(var army) => state.Players.Single(p => p.Id.Value == army.Value).IsEliminated,
            _ => throw new InvalidOperationException("Unreachable: unknown MissionCard archetype."),
        };

    private static bool ConquerContinentsSatisfied(
        GameState state, PlayerId player, IReadOnlyList<ContinentId> required, int wildcardCount)
    {
        var fullyOwned = Continents.All
            .Where(c => ContinentControl.IsFullyOwnedBy(c, state.Territories, player))
            .Select(c => c.Id)
            .ToHashSet();

        return required.All(fullyOwned.Contains)
            && fullyOwned.Count(id => !required.Contains(id)) >= wildcardCount;
    }
}
