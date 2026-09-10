using Risk.AI.Scoring;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Events;
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

    /// <summary>
    /// Cross product of Capital's full supported player-count range (3-5, per
    /// <see cref="Risk.Engine.Setup.GameSetup.PlayerCountRange"/>) and the
    /// three dice-sequence variants — mirrors <see cref="ClassicScenarios"/>
    /// exactly. <see cref="Risk.Engine.Setup.GameSetup.Create"/>'s own comment
    /// confirms Capital "reuses [Classic's Claim/Setup] path unchanged,"
    /// diverging only after Setup placement into
    /// <see cref="TurnPhase.SelectHeadquarters"/> instead of Reinforce — so
    /// driving from <see cref="GameHarness.Start"/> straight into
    /// <see cref="BotTurnRunner.RunGame"/> (no fast-forward needed) exercises
    /// the real <see cref="Risk.AI.Decisions.HeadquartersDecision"/>/<see cref="BotPlayer"/>
    /// stack through that phase for the first time end-to-end, exactly the
    /// same way 9a/9b exercised Claim/Setup/Reinforce/Attack/Fortify.
    /// </summary>
    public static IEnumerable<object[]> CapitalScenarios()
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
    /// Same zero-<c>Rejected</c> reasoning as the Classic/SecretMission tests
    /// above: reaching <see cref="BotRunResult.Completed"/> is itself
    /// sufficient proof of zero <c>Rejected</c> results anywhere in the run,
    /// including through the SelectHeadquarters phase this test is the first
    /// to drive with the real bot stack.
    /// </summary>
    [Theory]
    [MemberData(nameof(CapitalScenarios))]
    public void A_full_bot_vs_bot_Capital_game_reaches_a_valid_win_with_zero_rejected_commands(int playerCount, int diceVariant)
    {
        var dice = new SequenceDiceRoller(DiceSequenceVariants[diceVariant]);
        var harness = GameHarness.Start(GameMode.Capital, playerCount, dice);
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

        // Capital's real victory rule, called directly on the final state —
        // mirrors the Classic/SecretMission tests' pattern, so this
        // assertion can never silently drift from the rule it claims to
        // verify.
        Assert.Equal(won.Winner, new CapitalVictoryRule().CheckVictory(completed.State));

        // Independent re-verification, NOT delegating back to
        // CapitalVictoryRule: re-derive "holds own HQ AND every other active
        // player's revealed HQ" from the winner's OWN redacted PlayerView
        // (OwnHeadquarters/RevealedHeadquarters — exactly what a real
        // bot/AI client sees via Observe), cross-checked against territory
        // ownership on the final GameState. This is the batch's explicit
        // ask: don't just trust GameStatus.Won fired.
        var winnerView = harness.Engine.Observe(completed.State, won.Winner);
        Assert.NotNull(winnerView.OwnHeadquarters);
        Assert.Equal(completed.State.Players.Count, winnerView.RevealedHeadquarters.Count);
        Assert.Equal(won.Winner, completed.State.Territories[winnerView.OwnHeadquarters!.Value].Owner);
        foreach (var (_, hq) in winnerView.RevealedHeadquarters)
        {
            Assert.Equal(won.Winner, completed.State.Territories[hq].Owner);
        }
    }

    /// <summary>
    /// A single, hand-picked dice sequence (playerCount=4) that a wider
    /// exploratory search confirmed drives a genuine "the eventual winner's
    /// own headquarters gets captured by someone else, and that same winner
    /// later recaptures it" sequence during the real bot-vs-bot game — the
    /// exact path <see cref="BotWeights.RecaptureOwnHqWeight"/> (Phase 5's
    /// post-review fix) exists to prioritize. Length 17, prime, consistent
    /// with <see cref="DiceSequenceVariants"/>'s own convention.
    /// </summary>
    private static readonly IReadOnlyList<int> CapitalRecaptureDiceSequence =
        [4, 6, 2, 2, 5, 6, 6, 6, 4, 5, 3, 5, 6, 5, 4, 5, 6];

    /// <summary>
    /// Dedicated proof of Phase 5's post-review fix
    /// (<see cref="BotWeights.RecaptureOwnHqWeight"/>): this specific,
    /// deterministic replay must contain a genuine
    /// <see cref="HeadquartersCaptured"/> event where the eventual winner
    /// recaptures ITS OWN previously-lost headquarters
    /// (<c>Attacker == OriginalOwner == won.Winner</c>) en route to victory —
    /// not merely a game that happens to end in a win.
    ///
    /// <para>
    /// <b>Why observed opportunistically (via a hand-picked dice sequence),
    /// not hand-forced (via a spliced <see cref="GameState"/>):</b> Capital
    /// reuses Classic's Claim/Setup path, which (per
    /// <see cref="A_full_bot_vs_bot_Classic_game_reaches_a_valid_win_with_zero_rejected_commands"/>'s
    /// own remarks) never touches <c>Random.Shared</c> — territory dealing is
    /// fully bot-decided and deterministic for a given dice sequence and
    /// player count, so a wider search over exactly those two levers (the
    /// same two <see cref="ClassicScenarios"/>/<see cref="CapitalScenarios"/>
    /// already vary) is sufficient to reliably locate a scenario exhibiting
    /// this path — a search that found the sequence below among many
    /// candidates. Splicing ownership into a hand-edited
    /// <see cref="GameState"/> (as <see cref="GameHarness.WithMissions"/>
    /// does for missions) was considered and rejected: unlike a mission
    /// assignment, forcing HQ/territory ownership without going through the
    /// engine would desync <see cref="HeadquartersRevealed"/>/troop-total
    /// invariants the real engine enforces, producing a state no real game
    /// could ever reach — exactly what <see cref="GameHarness"/>'s own class
    /// doc says driving only the real engine exists to avoid. Once found,
    /// the sequence is pinned as a literal so this test is exactly as
    /// deterministic and reproducible as every other test in this file.
    /// </para>
    /// </summary>
    [Fact]
    public void A_Capital_winner_can_recapture_its_own_lost_headquarters_en_route_to_victory()
    {
        const int playerCount = 4;
        var dice = new SequenceDiceRoller(CapitalRecaptureDiceSequence);
        var harness = GameHarness.Start(GameMode.Capital, playerCount, dice);
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
        Assert.Equal(won.Winner, new CapitalVictoryRule().CheckVictory(completed.State));

        var selfRecaptured = completed.State.Log
            .OfType<HeadquartersCaptured>()
            .Any(e => e.Attacker == won.Winner && e.OriginalOwner == won.Winner);

        Assert.True(selfRecaptured,
            $"Expected winner {won.Winner} to have recaptured its own previously-lost headquarters " +
            "somewhere in this deterministic replay (that is this test's whole purpose) — if this ever " +
            "starts failing after a dice-content or weight retune, re-run the exploratory search and pin " +
            "a new CapitalRecaptureDiceSequence rather than deleting the assertion.");
    }

    /// <summary>
    /// TwoPlayer's own <c>Random.Shared</c>-based territory deal
    /// (<see cref="Risk.Engine.Modes.TwoPlayerSetupStrategy"/>, same lever
    /// <see cref="SecretMissionScenarios"/> already relies on for board
    /// variation) means each of these 3 dice-sequence variants already runs
    /// against a genuinely different random board — <see cref="ClassicScenarios"/>'s
    /// player-count dimension does not apply here, since
    /// <see cref="Risk.Engine.Setup.GameSetup.PlayerCountRange"/> fixes
    /// TwoPlayer at exactly 2.
    /// </summary>
    public static IEnumerable<object[]> TwoPlayerScenarios()
    {
        for (var variant = 0; variant < DiceSequenceVariants.Length; variant++)
        {
            yield return [variant];
        }
    }

    /// <summary>
    /// The fourth and final blocking DoD test (design D9, spec's "All four
    /// GameMode bot-vs-bot integration tests are blocking" requirement) — and
    /// the mode every prior phase's risk notes have flagged as the most
    /// fragile: it is the only mode with a synthetic third "neutral" army
    /// (<see cref="PlayerState.IsNeutral"/>) that never takes a real turn but
    /// must still be correctly identified by both real bots
    /// (<see cref="BotMemory.FindTwoPlayerNeutral"/>, design D8) so its
    /// territories are treated as attackable-but-passive rather than
    /// confused with the real opponent, and it has a unique two-phase Setup
    /// (Phase A: both humans place their own remaining troops; Phase B:
    /// humans place the NEUTRAL's remaining troops one at a time via
    /// <see cref="PlaceNeutralTroopsCommand"/>, detected via the 26-troop
    /// boundary). Same zero-<c>Rejected</c> reasoning as every other test in
    /// this file: reaching <see cref="BotRunResult.Completed"/> is itself
    /// sufficient proof of zero <c>Rejected</c> results anywhere in the run.
    /// </summary>
    [Theory]
    [MemberData(nameof(TwoPlayerScenarios))]
    public void A_full_bot_vs_bot_TwoPlayer_game_reaches_a_valid_win_with_zero_rejected_commands(int diceVariant)
    {
        const int playerCount = 2;
        var dice = new SequenceDiceRoller(DiceSequenceVariants[diceVariant]);
        var harness = GameHarness.Start(GameMode.TwoPlayer, playerCount, dice);
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
        Assert.False(winner.IsNeutral);

        // TwoPlayer's real victory rule, called directly on the final state —
        // mirrors the Classic/SecretMission/Capital tests' pattern, so this
        // assertion can never silently drift from the rule it claims to
        // verify.
        Assert.Equal(won.Winner, new TwoPlayerVictoryRule().CheckVictory(completed.State));

        // Independent re-verification, NOT delegating back to
        // TwoPlayerVictoryRule: TwoPlayer has exactly one real opponent, so
        // "won" and "the sole other real (non-neutral) player is eliminated"
        // are the same fact — cross-checked here directly from raw
        // PlayerState rather than trusting GameStatus.Won fired.
        var loser = completed.State.Players.Single(p => !p.IsNeutral && p.Id != won.Winner);
        Assert.True(loser.IsEliminated);

        // --- Setup Phase A/B boundary (design D8), exercised across a FULL
        // game, not just Phase 8's short synthetic sequence. Every
        // PlaceNeutralTroopsCommand SetupDecision emits places exactly 1
        // troop, so the neutral's own Setup budget (40 starting - 14 dealt =
        // 26, TwoPlayerSetupStrategy/GameSetup) must drain to EXACTLY 26
        // NeutralTroopsPlaced troops, and BOTH real seats must appear as a
        // Placer at least once — proving turn alternation genuinely
        // exercised both bots' SetupDecision Phase-B dispatch, not just one.
        var neutralPlacements = completed.State.Log.OfType<NeutralTroopsPlaced>().ToList();
        Assert.NotEmpty(neutralPlacements);
        Assert.Equal(26, neutralPlacements.Sum(e => e.Troops));
        Assert.All(bots, bot => Assert.Contains(neutralPlacements, e => e.Placer == bot.Id));

        // --- Neutral-by-elimination detection (design D8), proven to hold
        // for a full game's worth of turns: re-derive BOTH real bots'
        // inferred neutral identity directly from their OWN final
        // BotMemory (SeenActors accumulated over the entire game) using the
        // exact production function every SetupDecision/AttackDecision call
        // relied on throughout, and assert it matches the ACTUAL
        // engine-assigned neutral PlayerId.
        var actualNeutral = completed.State.Players.Single(p => p.IsNeutral).Id;
        foreach (var bot in bots)
        {
            var finalView = harness.Engine.Observe(completed.State, bot.Id);
            var inferredNeutral = completed.Memories[bot.Id].FindTwoPlayerNeutral(finalView, bot.Id);
            Assert.Equal(actualNeutral, inferredNeutral);
        }
    }
}
