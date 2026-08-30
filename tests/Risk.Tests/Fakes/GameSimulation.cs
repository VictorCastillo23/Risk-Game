using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Rules;
using Risk.Engine.State;

namespace Risk.Tests.Fakes;

/// <summary>
/// Shared full-game simulation helpers, extracted so integration-style tests
/// (a complete game driven turn-by-turn through <see cref="IGameEngine.Execute"/>)
/// don't reimplement the same placement/trade/attack/fortify strategies —
/// see <c>Risk.Tests.Engine.FullGameIntegrationTests</c> and
/// <c>Risk.Tests.Engine.ClaimTerritoryCommandTests</c> for callers.
/// </summary>
internal static class GameSimulation
{
    public static GameState PlaceOneStartingTroop(GameEngine engine, GameState state)
    {
        var actor = state.Turn.CurrentPlayer;
        var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
        var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new PlaceTroopsCommand(actor, territory, 1)));
        return result.State;
    }

    /// <summary>Piles all Reinforce-phase troops onto whichever owned territory currently has the most troops, keeping the main army concentrated.</summary>
    public static GameState PlaceReinforcementOnStrongestTerritory(GameEngine engine, GameState state)
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
    /// by the end-of-turn card draw never idles above the mandatory-trade-in
    /// threshold.
    /// </summary>
    public static GameState TradeAllAvailableSets(GameEngine engine, GameState state, Risk.Domain.Players.PlayerId actor)
    {
        while (TryFindValidSet(state.Players.Single(p => p.Id == actor).Hand, out var set))
        {
            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new TradeCardsCommand(actor, set)));
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

    public static GameState EndPhase(GameEngine engine, GameState state, Risk.Domain.Players.PlayerId actor)
    {
        var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(engine.Execute(state, new EndPhaseCommand(actor)));
        return result.State;
    }

    /// <summary>
    /// Issues the attack and, on conquest, immediately resolves the
    /// resulting <see cref="PendingOccupation"/> by moving only the minimum
    /// required troops, so the attacking army's strength stays concentrated
    /// at its source for the next attack.
    /// </summary>
    public static GameState AttackAndOccupy(GameEngine engine, GameState state, Risk.Domain.Players.PlayerId actor, TerritoryId from, TerritoryId to)
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
    public static bool TryFindAttack(GameState state, Risk.Domain.Players.PlayerId actor, out TerritoryId from, out TerritoryId to)
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
    /// Spends this turn's single Fortify move relocating the strongest owned
    /// territory's army toward a "frontier" territory: one
    /// <paramref name="actor"/> owns that borders an enemy but is too weak
    /// (1 troop) to attack from. Tries every reservoir/frontier pair
    /// (strongest reservoirs first) until one is connected by a friendly
    /// path; does nothing if none currently are (the engine rejects
    /// unconnected attempts without spending the once-per-turn fortify).
    /// </summary>
    public static GameState TryFortifyTowardAFrontier(GameEngine engine, Risk.Domain.Players.PlayerId actor, GameState state)
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
