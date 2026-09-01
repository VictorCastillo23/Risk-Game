using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Rules;
using Risk.Engine.State;
using Risk.Web.Models;
using Risk.Web.Services;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Services;

/// <summary>
/// End-to-end proof that the Blazor-facing session wrapper
/// (<see cref="GameSessionService"/>), not just the raw engine, carries a
/// real 2-player game all the way to a <see cref="GameStatus.Won"/> state —
/// task 10.4. Mirrors <c>Risk.Tests.Engine.FullGameIntegrationTests</c>'s
/// script exactly, but every command is dispatched through
/// <see cref="GameSessionService.Execute"/>/<see cref="GameSessionService.Start"/>
/// instead of <see cref="IGameEngine.Execute"/> directly, so this test also
/// exercises the seam <c>Game.razor</c>/<c>VictoryScreen.razor</c> actually
/// depend on: the <c>Changed</c> event, the <c>PlayerSetupRow</c>→<c>PlayerId</c>
/// zip, and <c>ConfigFor</c> (which <c>VictoryScreen</c> uses to resolve the
/// winner's display name).
/// </summary>
public class GameSessionServiceFullGameIntegrationTests
{
    private const int MaxCommands = 5_000; // safety net: fail loudly instead of hanging if conquest ever stalls

    [Fact]
    public void A_full_two_player_game_reaches_victory_through_GameSessionService()
    {
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());
        // TwoPlayer mode never rolls the setup dice (TurnOrder.DetermineFirst
        // is only invoked for Classic), so reusing the combat roller here is
        // safe and avoids introducing an unused third fake.
        var session = new GameSessionService(engine, new AlwaysAttackerWinsDiceRoller());
        var changedCount = 0;
        session.Changed += () => changedCount++;

        var rows = new List<PlayerSetupRow>
        {
            new("Ana", "#E53935", false),
            new("Beto", "#1E88E5", false)
        };
        var startResult = session.Start(rows, GameMode.TwoPlayer);
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(startResult);

        var attacker = session.State!.Turn.CurrentPlayer; // player 0 is always dealt the first turn
        var humanOpponent = session.State!.Players.Single(p => !p.IsNeutral && p.Id != attacker).Id;

        // Setup phase: place every starting troop, one at a time, in rotation.
        while (session.State!.Turn.Phase == TurnPhase.Setup)
        {
            PlaceOneStartingTroop(session);
        }

        var commandsIssued = 0;
        while (session.State!.Status is not GameStatus.Won)
        {
            Assert.True(++commandsIssued < MaxCommands, "Full-game script exceeded the safety command cap without reaching victory.");

            var state = session.State!;
            var actor = state.Turn.CurrentPlayer;

            if (state.Turn.Phase == TurnPhase.Reinforce)
            {
                if (actor == attacker)
                {
                    TradeAllAvailableSets(session, actor);
                    PlaceReinforcementOnStrongestTerritory(session, actor);
                }
                else
                {
                    PlaceAllReinforcementTroops(session, actor);
                }

                EndPhase(session, actor);
                continue;
            }

            if (state.Turn.Phase == TurnPhase.Attack)
            {
                if (actor == attacker && TryFindAttack(state, actor, out var from, out var to))
                {
                    AttackAndOccupy(session, actor, from, to);
                    continue;
                }

                EndPhase(session, actor);
                continue;
            }

            if (state.Turn.Phase == TurnPhase.Fortify)
            {
                if (actor == attacker)
                {
                    TryFortifyTowardAFrontier(session, actor);
                }

                EndPhase(session, actor);
                continue;
            }
        }

        var finalState = session.State!;
        var won = Assert.IsType<GameStatus.Won>(finalState.Status);
        Assert.Equal(attacker, won.Winner);
        // Victory now triggers as soon as the human opponent is eliminated (item
        // 4.3's TwoPlayerVictoryRule) — the neutral may still own territory at
        // that point, so this no longer asserts the attacker owns all 42.
        var opponentFinal = finalState.Players.Single(p => p.Id == humanOpponent);
        Assert.True(opponentFinal.IsEliminated);
        Assert.Contains(finalState.Log, e => e is GameWon);
        Assert.True(changedCount > 0);

        // VictoryScreen's Winner parameter comes from Session.ConfigFor(won.Winner) — prove it resolves correctly.
        var winnerConfig = session.ConfigFor(won.Winner);
        Assert.Equal("Ana", winnerConfig.Name);

        // Once won, GameSessionService must propagate the engine's terminal
        // GameOver rejection without mutating State (mirrors the raw-engine
        // test's own final assertion, one layer up through the session seam).
        var stateBeforeRejectedCommand = session.State;
        var rejected = session.Execute(new EndPhaseCommand(attacker));
        Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(rejected);
        Assert.Same(stateBeforeRejectedCommand, session.State);
    }

    /// <summary>
    /// TwoPlayer-aware (item 4.1) — mirrors
    /// <c>Risk.Tests.Fakes.GameSimulation.PlaceOneStartingTroop</c>: once the
    /// acting human's own Setup pool is drained while Setup is still active,
    /// Phase B is open and they instead choose where one of the neutral's
    /// troops lands via <see cref="PlaceNeutralTroopsCommand"/>.
    /// </summary>
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

    /// <summary>Piles all Reinforce-phase troops onto whichever owned territory currently has the most troops, keeping the main army concentrated.</summary>
    private static void PlaceReinforcementOnStrongestTerritory(GameSessionService session, PlayerId actor)
    {
        while (session.State!.Players.Single(p => p.Id == actor).TroopsRemaining > 0)
        {
            var state = session.State!;
            var territory = state.Territories
                .Where(kv => kv.Value.Owner == actor)
                .OrderByDescending(kv => kv.Value.Troops)
                .First().Key;
            var result = session.Execute(new PlaceTroopsCommand(actor, territory, 1));
            Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        }
    }

    /// <summary>Places all of the passive player's Reinforce-phase troops, one at a time, on whichever territory they own first in enumeration order.</summary>
    private static void PlaceAllReinforcementTroops(GameSessionService session, PlayerId actor)
    {
        while (session.State!.Players.Single(p => p.Id == actor).TroopsRemaining > 0)
        {
            var state = session.State!;
            var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
            var result = session.Execute(new PlaceTroopsCommand(actor, territory, 1));
            Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        }
    }

    /// <summary>Repeatedly trades in any valid 3-card set found in <paramref name="actor"/>'s hand until none remains.</summary>
    private static void TradeAllAvailableSets(GameSessionService session, PlayerId actor)
    {
        while (TryFindValidSet(session.State!.Players.Single(p => p.Id == actor).Hand, out var set))
        {
            var result = session.Execute(new TradeCardsCommand(actor, set));
            Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        }
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

    private static void EndPhase(GameSessionService session, PlayerId actor)
    {
        var result = session.Execute(new EndPhaseCommand(actor));
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
    }

    /// <summary>Issues the attack and, on conquest, immediately resolves the resulting <see cref="PendingOccupation"/> by moving only the minimum required troops.</summary>
    private static void AttackAndOccupy(GameSessionService session, PlayerId actor, TerritoryId from, TerritoryId to)
    {
        var attackRawResult = session.Execute(new AttackCommand(actor, from, to, 1));
        if (attackRawResult is CommandResult<GameState, GameEvent>.Rejected rejectedAttack)
        {
            Assert.Fail($"Attack {from}->{to} by {actor} rejected: {rejectedAttack.Error.Code} - {rejectedAttack.Error.Message}.");
        }
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(attackRawResult);

        var state = session.State!;
        if (state.Status is GameStatus.Won)
        {
            return; // this conquest was also the winning move; no occupation left to resolve
        }

        var pending = state.Turn.PendingOccupation;
        if (pending is null)
        {
            return; // this round didn't finish the conquest yet; keep attacking the same pair
        }

        var occupyRawResult = session.Execute(new OccupyCommand(actor, pending.MinimumTroops));
        if (occupyRawResult is CommandResult<GameState, GameEvent>.Rejected rejectedOccupy)
        {
            Assert.Fail($"Occupy {pending.From}->{pending.Conquered} min={pending.MinimumTroops} by {actor} rejected: {rejectedOccupy.Error.Code} - {rejectedOccupy.Error.Message}.");
        }
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(occupyRawResult);
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

    /// <summary>Spends this turn's single Fortify move relocating the strongest owned territory's army toward a frontier territory that borders an enemy but is too weak to attack from.</summary>
    private static void TryFortifyTowardAFrontier(GameSessionService session, PlayerId actor)
    {
        var state = session.State!;
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
                var result = session.Execute(new FortifyCommand(actor, reservoir, frontier, troopsToMove));
                if (result is CommandResult<GameState, GameEvent>.Ok)
                {
                    return;
                }
            }
        }
    }
}
