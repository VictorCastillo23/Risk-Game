using Risk.Domain.Errors;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Web.Models;
using Risk.Web.Services;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Services;

/// <summary>
/// D1's load-bearing parity oracle: at every Setup step of a real TwoPlayer
/// game, <see cref="TwoPlayerSetupPhase.IsPhaseB"/>'s verdict must agree with
/// whether the real engine actually accepts a
/// <see cref="PlaceNeutralTroopsCommand"/> — bidirectional, so any future
/// drift between the Web mirror and <c>GameEngine.IsPhaseB</c>
/// (<c>src/Risk.Engine/GameEngine.cs:200-204</c>) fails this test instead of
/// shipping a confusing board. Also proves the surrounding Phase A/B/Reinforce
/// shape the design depends on: Phase A rotates every 2 troops, Phase B opens
/// exactly when both humans' pools hit 0, and Setup exits to Reinforce once
/// the neutral's own pool is drained.
/// </summary>
public class TwoPlayerSetupPhaseParityTests
{
    [Fact]
    public void PlaceNeutralTroopsCommand_IsAcceptedIfAndOnlyIf_IsPhaseB_AtEverySetupStep()
    {
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var session = new GameSessionService(engine, new AlwaysAttackerWinsDiceRoller());
        var rows = new List<PlayerSetupRow>
        {
            new("Ana", "#E53935", false),
            new("Beto", "#1E88E5", false)
        };

        var startResult = session.Start(rows, GameMode.TwoPlayer);
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(startResult);

        var phaseAActors = new List<PlayerId>();
        var sawPhaseA = false;
        var sawPhaseB = false;

        while (session.State!.Turn.Phase == TurnPhase.Setup)
        {
            var state = session.State!;
            var actor = state.Turn.CurrentPlayer;
            var isPhaseB = TwoPlayerSetupPhase.IsPhaseB(state);
            var neutral = state.Players.Single(p => p.IsNeutral);
            var neutralTerritory = state.Territories.First(kv => kv.Value.Owner == neutral.Id).Key;

            // Probe: does the real engine accept PlaceNeutralTroopsCommand
            // right now? Executed against `state` (a value, immutably
            // returned by GameEngine.Execute) without ever touching
            // `session`, so this never disturbs the actual game progression
            // below.
            var probe = engine.Execute(state, new PlaceNeutralTroopsCommand(actor, neutralTerritory, 1));

            if (isPhaseB)
            {
                sawPhaseB = true;
                Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(probe);

                var result = session.Execute(new PlaceNeutralTroopsCommand(actor, neutralTerritory, 1));
                Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
            }
            else
            {
                sawPhaseA = true;
                phaseAActors.Add(actor);
                var rejected = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(probe);
                Assert.Equal(GameErrorCode.WrongPhase, rejected.Error.Code);

                var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
                var result = session.Execute(new PlaceTroopsCommand(actor, territory, 1));
                Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
            }
        }

        Assert.True(sawPhaseA, "Expected the script to pass through Phase A at least once.");
        Assert.True(sawPhaseB, "Expected the script to pass through Phase B at least once.");

        // Phase A rotates the current player every 2 troops (1 click each).
        for (var i = 0; i < phaseAActors.Count; i += 2)
        {
            Assert.Equal(phaseAActors[i], phaseAActors[i + 1]);
            if (i + 2 < phaseAActors.Count)
            {
                Assert.NotEqual(phaseAActors[i], phaseAActors[i + 2]);
            }
        }

        // Setup exits to Reinforce only once the neutral's own pool is
        // empty (the last Phase-B placement drains it). By this point the
        // entering player has already received their Reinforce-phase troop
        // allotment (continent/territory bonus), so TroopsRemaining on the
        // humans here reflects that bonus, not Setup leftovers — the "both
        // humans were drained before Phase B" invariant is already proven
        // by construction: every Phase-B iteration above only ran because
        // TwoPlayerSetupPhase.IsPhaseB(state) was true, which itself
        // requires both humans' Setup pools to be 0.
        var finalState = session.State!;
        Assert.Equal(TurnPhase.Reinforce, finalState.Turn.Phase);
        Assert.Equal(0, finalState.Players.Single(p => p.IsNeutral).TroopsRemaining);
    }
}
