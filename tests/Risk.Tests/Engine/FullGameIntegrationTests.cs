using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Rules;
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
        var setupOk = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(GameSetup.Create(2));
        var state = setupOk.State;
        var attacker = state.Turn.CurrentPlayer; // player 0 is always dealt the first turn

        // Setup phase: place every starting troop, one at a time, in rotation.
        while (state.Turn.Phase == TurnPhase.Setup)
        {
            state = PlaceOneStartingTroop(engine, state);
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
                    state = TradeAllAvailableSets(engine, state, actor);
                    state = PlaceReinforcementOnStrongestTerritory(engine, state);
                }
                else
                {
                    state = GameStateBuilder.PlaceAllReinforcementTroops(state, engine);
                }

                state = EndPhase(engine, state, actor);
                continue;
            }

            if (state.Turn.Phase == TurnPhase.Attack)
            {
                if (actor == attacker && TryFindAttack(state, actor, out var from, out var to))
                {
                    state = AttackAndOccupy(engine, state, actor, from, to);
                    continue;
                }

                state = EndPhase(engine, state, actor);
                continue;
            }

            if (state.Turn.Phase == TurnPhase.Fortify)
            {
                if (actor == attacker)
                {
                    state = TryFortifyTowardAFrontier(engine, actor, state);
                }

                state = EndPhase(engine, state, actor);
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

    private static GameState PlaceOneStartingTroop(GameEngine engine, GameState state)
    {
        var actor = state.Turn.CurrentPlayer;
        var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
        var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new PlaceTroopsCommand(actor, territory, 1)));
        return result.State;
    }

    /// <summary>Piles all Reinforce-phase troops onto whichever owned territory currently has the most troops, keeping the main army concentrated.</summary>
    private static GameState PlaceReinforcementOnStrongestTerritory(GameEngine engine, GameState state)
    {
        var actor = state.Turn.CurrentPlayer;

        while (state.Players.Single(p => p.Id == actor).TroopsRemaining > 0)
        {
            var territory = state.Territories
                .Where(kv => kv.Value.Owner == actor)
                .OrderByDescending(kv => kv.Value.Troops)
                .First().Key;
            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new PlaceTroopsCommand(actor, territory, 1)));
            state = result.State;
        }

        return state;
    }

    /// <summary>
    /// Repeatedly trades in any valid 3-card set found in
    /// <paramref name="actor"/>'s hand until none remains, so a hand grown
    /// by the end-of-turn card draw (PR8) never idles above the
    /// mandatory-trade-in threshold.
    /// </summary>
    private static GameState TradeAllAvailableSets(GameEngine engine, GameState state, PlayerId actor)
    {
        while (TryFindValidSet(state.Players.Single(p => p.Id == actor).Hand, out var set))
        {
            // Any owned-territory match is an acceptable pick here: the test
            // only cares that the mandatory trade-in succeeds, not which
            // territory receives the occupied-territory bonus.
            var matches = TerritoryTradeBonus.ResolveMatches(set, state.Territories, actor);
            TerritoryId? bonusTerritory = matches.Count > 0 ? matches[0] : null;

            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new TradeCardsCommand(actor, set, bonusTerritory)));
            state = result.State;
        }

        return state;
    }

    /// <summary>Brute-force search over every 3-card combination in <paramref name="hand"/> for one that forms a valid trade-in set.</summary>
    private static bool TryFindValidSet(IReadOnlyList<Card> hand, out IReadOnlyList<Card> set)
    {
        for (var i = 0; i < hand.Count; i++)
        {
            for (var j = i + 1; j < hand.Count; j++)
            {
                for (var k = j + 1; k < hand.Count; k++)
                {
                    var candidate = new[] { hand[i], hand[j], hand[k] };
                    if (CardSet.IsValid(candidate))
                    {
                        set = candidate;
                        return true;
                    }
                }
            }
        }

        set = Array.Empty<Card>();
        return false;
    }

    private static GameState EndPhase(GameEngine engine, GameState state, PlayerId actor)
    {
        var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(engine.Execute(state, new EndPhaseCommand(actor)));
        return result.State;
    }

    /// <summary>
    /// Issues the attack and, on conquest, immediately resolves the
    /// resulting <see cref="PendingOccupation"/> by moving only the
    /// minimum required troops, so the attacking army's strength stays
    /// concentrated at its source for the next attack.
    /// </summary>
    private static GameState AttackAndOccupy(GameEngine engine, GameState state, PlayerId actor, TerritoryId from, TerritoryId to)
    {
        var attackRawResult = engine.Execute(state, new AttackCommand(actor, from, to, 1));
        if (attackRawResult is CommandResult<GameState, GameEvent>.Rejected rejectedAttack)
        {
            Assert.Fail($"Attack {from}->{to} by {actor} rejected: {rejectedAttack.Error.Code} - {rejectedAttack.Error.Message}. FromTroops={state.Territories[from].Troops} FromOwner={state.Territories[from].Owner} ToOwner={state.Territories[to].Owner} Phase={state.Turn.Phase} CurrentPlayer={state.Turn.CurrentPlayer} Pending={state.Turn.PendingOccupation}");
        }
        var attackResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(attackRawResult);
        state = attackResult.State;

        if (state.Status is GameStatus.Won)
        {
            return state; // this conquest was also the winning move; no occupation left to resolve
        }

        var pending = state.Turn.PendingOccupation;
        if (pending is null)
        {
            return state; // this round didn't finish the conquest yet; keep attacking the same pair
        }

        var occupyRawResult = engine.Execute(state, new OccupyCommand(actor, pending.MinimumTroops));
        if (occupyRawResult is CommandResult<GameState, GameEvent>.Rejected rejectedOccupy)
        {
            Assert.Fail($"Occupy {pending.From}->{pending.Conquered} min={pending.MinimumTroops} by {actor} rejected: {rejectedOccupy.Error.Code} - {rejectedOccupy.Error.Message}. SourceTroops={state.Territories[pending.From].Troops}");
        }
        var occupyResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(occupyRawResult);
        return occupyResult.State;
    }

    /// <summary>Finds any territory <paramref name="actor"/> owns with 2+ troops that borders a territory owned by someone else.</summary>
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

    /// <summary>
    /// Spends this turn's single Fortify move relocating the strongest
    /// owned territory's army toward a "frontier" territory: one
    /// <paramref name="actor"/> owns that borders an enemy but is too weak
    /// (1 troop) to attack from. Tries every reservoir/frontier pair
    /// (strongest reservoirs first) until one is connected by a friendly
    /// path; does nothing if none currently are (the engine rejects
    /// unconnected attempts without spending the once-per-turn fortify).
    /// </summary>
    private static GameState TryFortifyTowardAFrontier(GameEngine engine, PlayerId actor, GameState state)
    {
        var reservoirs = state.Territories
            .Where(kv => kv.Value.Owner == actor && kv.Value.Troops >= 2)
            .OrderByDescending(kv => kv.Value.Troops)
            .Select(kv => kv.Key)
            .ToArray();

        var frontiers = state.Territories
            .Where(kv => kv.Value.Owner == actor && kv.Value.Troops < 2
                && WorldMap.NeighborsOf(kv.Key).Any(n => state.Territories[n].Owner != actor))
            .Select(kv => kv.Key)
            .ToArray();

        foreach (var reservoir in reservoirs)
        {
            foreach (var frontier in frontiers)
            {
                if (reservoir == frontier)
                {
                    continue;
                }

                var troopsToMove = state.Territories[reservoir].Troops - 1;
                var result = engine.Execute(state, new FortifyCommand(actor, reservoir, frontier, troopsToMove));
                if (result is CommandResult<GameState, GameEvent>.Ok ok)
                {
                    return ok.State;
                }
            }
        }

        return state;
    }
}
