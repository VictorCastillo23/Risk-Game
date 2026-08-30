using Risk.Domain.Map;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Setup;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

/// <summary>
/// End-to-end proof that all 7 PRs compose: drives a real 2-player game
/// from <see cref="GameSetup.Create"/> (real random territory deal) all the
/// way to a <see cref="GameStatus.Won"/> state, purely through the public
/// <see cref="IGameEngine.Execute"/> command pipeline with a deterministic
/// <see cref="AlwaysAttackerWinsDiceRoller"/> fake standing in for real
/// dice. Only the attacking player (player 0) ever issues
/// <see cref="AttackCommand"/>s, so conquest is monotonic; player 1 plays a
/// purely passive game (reinforce, then pass through Attack/Fortify),
/// guaranteeing player 0 eventually owns all 42 territories.
///
/// Conquest strategy: each successful attack moves only the minimum troops
/// required into the newly conquered territory, preserving the attacking
/// army's strength; when the current army runs out of directly-adjacent
/// enemies, a single once-per-turn <see cref="FortifyCommand"/> relocates
/// it (via a friendly-owned chain, not just a direct neighbor) to whichever
/// owned frontier territory still borders an enemy but is too weak (1
/// troop) to attack from, letting the army effectively walk the whole
/// connected map turn by turn until nothing is left to conquer.
///
/// Card handling (PR8): the attacker draws a card at the end of any turn
/// in which they conquered a territory, so before placing reinforcement
/// each turn they opportunistically trade in any valid 3-card set found in
/// their hand, keeping it below the mandatory-trade-in threshold.
/// </summary>
public class FullGameIntegrationTests
{
    private const int MaxCommands = 5_000; // safety net: fail loudly instead of hanging if conquest ever stalls

    [Fact]
    public void A_full_two_player_game_reaches_victory_through_the_public_command_pipeline()
    {
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        var setupOk = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(2, GameMode.TwoPlayer, QueuedDiceRoller.ForRollOff(2)));
        var state = setupOk.State;
        var attacker = state.Turn.CurrentPlayer; // player 0 is always dealt the first turn

        // Setup phase: place every starting troop, one at a time, in rotation.
        while (state.Turn.Phase == TurnPhase.Setup)
        {
            state = GameSimulation.PlaceOneStartingTroop(engine, state);
        }

        var commandsIssued = 0;
        while (state.Status is not GameStatus.Won)
        {
            Assert.True(++commandsIssued < MaxCommands, "Full-game script exceeded the safety command cap without reaching victory.");

            var actor = state.Turn.CurrentPlayer;

            if (state.Turn.Phase == TurnPhase.Reinforce)
            {
                if (actor == attacker)
                {
                    // The attacker is the only player who ever conquers, so
                    // only their hand grows via the end-of-turn card draw
                    // (PR8). Trade down any valid set before placing, both
                    // to stay realistic and to avoid ever tripping the
                    // mandatory-trade-in gate (>=5 cards) at the top of the
                    // Reinforce phase.
                    state = GameSimulation.TradeAllAvailableSets(engine, state, actor);
                    state = GameSimulation.PlaceReinforcementOnStrongestTerritory(engine, state);
                }
                else
                {
                    state = GameStateBuilder.PlaceAllReinforcementTroops(state, engine);
                }

                state = GameSimulation.EndPhase(engine, state, actor);
                continue;
            }

            if (state.Turn.Phase == TurnPhase.Attack)
            {
                if (actor == attacker && GameSimulation.TryFindAttack(state, actor, out var from, out var to))
                {
                    state = GameSimulation.AttackAndOccupy(engine, state, actor, from, to);
                    continue;
                }

                state = GameSimulation.EndPhase(engine, state, actor);
                continue;
            }

            if (state.Turn.Phase == TurnPhase.Fortify)
            {
                if (actor == attacker)
                {
                    state = GameSimulation.TryFortifyTowardAFrontier(engine, actor, state);
                }

                state = GameSimulation.EndPhase(engine, state, actor);
                continue;
            }
        }

        var won = Assert.IsType<GameStatus.Won>(state.Status);
        Assert.Equal(attacker, won.Winner);
        Assert.All(state.Territories.Values, t => Assert.Equal(attacker, t.Owner));
        Assert.Equal(WorldMap.Territories.Count, state.Territories.Count(t => t.Value.Owner == attacker));
        Assert.Contains(state.Log, e => e is GameWon);

        // Once won, the engine must refuse any further command.
        var rejected = engine.Execute(state, new EndPhaseCommand(attacker));
        Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(rejected);
    }

}
