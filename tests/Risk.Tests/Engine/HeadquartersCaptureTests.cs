using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

/// <summary>
/// Covers the additive <see cref="HeadquartersCaptured"/> guard in
/// <c>GameEngine.ExecuteAttack</c> (roadmap 5.2). Starts every scenario from
/// <see cref="GameStateBuilder.CompleteSetup"/> (real Claim/Setup/
/// SelectHeadquarters flow, so <c>PlayerState.HeadquartersId</c> is
/// genuinely set), then reshapes specific territory entries directly via
/// <c>state with { Territories = ... }</c> plus a hand-set <see cref="TurnState"/>
/// to drive conquest, rather than replaying full attack/occupy sequences —
/// the only practical way to reach a second recapture hop without also
/// resolving <c>PendingOccupation</c> and rotating turns (design D6).
/// </summary>
public class HeadquartersCaptureTests
{
    [Fact]
    public void Execute_emits_HeadquartersCaptured_after_TerritoryConquered_when_the_defender_keeps_other_territory()
    {
        var baseState = GameStateBuilder.CompleteSetup(3, GameMode.Capital);
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var hq = baseState.Players.Single(p => p.Id == a).HeadquartersId!.Value;
        var attackFrom = WorldMap.NeighborsOf(hq).First();

        var territories = new Dictionary<TerritoryId, TerritoryState>(baseState.Territories)
        {
            [attackFrom] = new TerritoryState(b, 5),
            [hq] = new TerritoryState(a, 1)
        };
        var state = baseState with { Territories = territories, Turn = new TurnState(b, TurnPhase.Attack) };
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());

        var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new AttackCommand(b, attackFrom, hq, 2)));

        var eventList = result.Events.ToList();
        var conqueredIndex = eventList.FindIndex(e => e is TerritoryConquered);
        var capturedIndex = eventList.FindIndex(e => e is HeadquartersCaptured);
        Assert.True(conqueredIndex >= 0, "TerritoryConquered must fire.");
        Assert.True(capturedIndex >= 0, "HeadquartersCaptured must fire.");
        Assert.True(conqueredIndex < capturedIndex, "HeadquartersCaptured must fire after TerritoryConquered.");

        var captured = Assert.IsType<HeadquartersCaptured>(eventList.Single(e => e is HeadquartersCaptured));
        Assert.Equal(b, captured.Attacker);
        Assert.Equal(a, captured.OriginalOwner);
        Assert.Equal(hq, captured.Territory);

        Assert.Contains(result.State.Log, e => e is HeadquartersCaptured);
        Assert.False(result.State.Players.Single(p => p.Id == a).IsEliminated);
    }

    [Fact]
    public void Execute_recapture_chain_keeps_OriginalOwner_as_the_first_declarer_not_the_intermediate_holder()
    {
        // CRUX (design D1): a naive implementation reading
        // `defenderTerritory.Owner` instead of scanning all players'
        // HeadquartersId would pass every other test but fail this one,
        // reporting B as OriginalOwner on the second hop instead of A.
        var baseState = GameStateBuilder.CompleteSetup(3, GameMode.Capital);
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var c = new PlayerId(2);
        var hq = baseState.Players.Single(p => p.Id == a).HeadquartersId!.Value;
        var attackFrom = WorldMap.NeighborsOf(hq).First();

        // Strip A down to owning only their HQ, so hop 1 both captures the
        // HQ and eliminates A — covering "an eliminated player's HQ remains
        // detectable" (spec scenario 3) in the same fixture.
        var territories = new Dictionary<TerritoryId, TerritoryState>(baseState.Territories);
        foreach (var (id, ts) in baseState.Territories)
        {
            if (ts.Owner == a && id != hq)
            {
                territories[id] = ts with { Owner = b };
            }
        }

        territories[hq] = new TerritoryState(a, 1);
        territories[attackFrom] = new TerritoryState(b, 5);

        var state = baseState with { Territories = territories, Turn = new TurnState(b, TurnPhase.Attack) };
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());

        // Hop 1: B captures A's HQ; A owns nothing else, so A is eliminated.
        var firstResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new AttackCommand(b, attackFrom, hq, 2)));

        var firstCaptured = Assert.IsType<HeadquartersCaptured>(
            Assert.Single(firstResult.Events, e => e is HeadquartersCaptured));
        Assert.Equal(b, firstCaptured.Attacker);
        Assert.Equal(a, firstCaptured.OriginalOwner);
        Assert.True(firstResult.State.Players.Single(p => p.Id == a).IsEliminated);

        // Hop 2: C recaptures the same HQ from B. Reshape directly (design
        // D6) instead of replaying OccupyCommand.
        var secondTerritories = new Dictionary<TerritoryId, TerritoryState>(firstResult.State.Territories)
        {
            [hq] = new TerritoryState(b, 1),
            [attackFrom] = new TerritoryState(c, 5)
        };
        var secondState = firstResult.State with
        {
            Territories = secondTerritories,
            Turn = new TurnState(c, TurnPhase.Attack)
        };

        var secondResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(secondState, new AttackCommand(c, attackFrom, hq, 2)));

        var secondCaptured = Assert.IsType<HeadquartersCaptured>(
            Assert.Single(secondResult.Events, e => e is HeadquartersCaptured));
        Assert.Equal(c, secondCaptured.Attacker);
        Assert.Equal(a, secondCaptured.OriginalOwner); // must stay A, never B
        Assert.Equal(hq, secondCaptured.Territory);
    }

    [Fact]
    public void Execute_does_not_emit_HeadquartersCaptured_when_the_conquered_territory_is_not_any_declared_HQ()
    {
        var baseState = GameStateBuilder.CompleteSetup(3, GameMode.Capital);
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var hqIds = baseState.Players.Select(p => p.HeadquartersId!.Value).ToHashSet();
        var (from, to) = FindNonHqAdjacentPair(hqIds);

        var territories = new Dictionary<TerritoryId, TerritoryState>(baseState.Territories)
        {
            [from] = new TerritoryState(b, 5),
            [to] = new TerritoryState(a, 1)
        };
        var state = baseState with { Territories = territories, Turn = new TurnState(b, TurnPhase.Attack) };
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());

        var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new AttackCommand(b, from, to, 2)));

        Assert.Contains(result.Events, e => e is TerritoryConquered);
        Assert.DoesNotContain(result.Events, e => e is HeadquartersCaptured);
    }

    [Fact]
    public void Execute_emits_HeadquartersCaptured_and_eliminates_the_owner_when_the_HQ_is_their_last_territory()
    {
        var baseState = GameStateBuilder.CompleteSetup(3, GameMode.Capital);
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var hq = baseState.Players.Single(p => p.Id == a).HeadquartersId!.Value;
        var attackFrom = WorldMap.NeighborsOf(hq).First();

        var territories = new Dictionary<TerritoryId, TerritoryState>(baseState.Territories);
        foreach (var (id, ts) in baseState.Territories)
        {
            if (ts.Owner == a && id != hq)
            {
                territories[id] = ts with { Owner = b };
            }
        }

        territories[hq] = new TerritoryState(a, 1);
        territories[attackFrom] = new TerritoryState(b, 5);

        var state = baseState with { Territories = territories, Turn = new TurnState(b, TurnPhase.Attack) };
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());

        var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new AttackCommand(b, attackFrom, hq, 2)));

        var captured = Assert.IsType<HeadquartersCaptured>(
            Assert.Single(result.Events, e => e is HeadquartersCaptured));
        Assert.Equal(a, captured.OriginalOwner);
        Assert.Equal(hq, captured.Territory);

        var eliminated = Assert.IsType<PlayerEliminated>(
            Assert.Single(result.Events, e => e is PlayerEliminated));
        Assert.Equal(a, eliminated.Victim);
        Assert.True(result.State.Players.Single(p => p.Id == a).IsEliminated);
    }

    [Theory]
    [InlineData(GameMode.Classic)]
    [InlineData(GameMode.SecretMission)]
    public void Execute_does_not_emit_HeadquartersCaptured_outside_GameMode_Capital(GameMode mode)
    {
        var baseState = GameStateBuilder.CompleteSetup(3, mode);
        var a = new PlayerId(0);
        var b = new PlayerId(1);
        var target = baseState.Territories.Keys.First();
        var attackFrom = WorldMap.NeighborsOf(target).First();

        var territories = new Dictionary<TerritoryId, TerritoryState>(baseState.Territories)
        {
            [attackFrom] = new TerritoryState(b, 5),
            [target] = new TerritoryState(a, 1)
        };
        var state = baseState with { Territories = territories, Turn = new TurnState(b, TurnPhase.Attack) };
        var engine = new GameEngine(new AlwaysAttackerWinsDiceRoller());

        var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            engine.Execute(state, new AttackCommand(b, attackFrom, target, 2)));

        Assert.Contains(result.Events, e => e is TerritoryConquered);
        Assert.DoesNotContain(result.Events, e => e is HeadquartersCaptured);
        Assert.All(baseState.Players, p => Assert.Null(p.HeadquartersId));
    }

    /// <summary>
    /// Finds an adjacent territory pair where neither side is any player's
    /// declared HQ, so conquering it must not trigger the capture guard.
    /// </summary>
    private static (TerritoryId From, TerritoryId To) FindNonHqAdjacentPair(IReadOnlySet<TerritoryId> hqIds)
    {
        foreach (var territory in WorldMap.Territories)
        {
            if (hqIds.Contains(territory.Id))
            {
                continue;
            }

            foreach (var neighbor in WorldMap.NeighborsOf(territory.Id))
            {
                if (!hqIds.Contains(neighbor))
                {
                    return (territory.Id, neighbor);
                }
            }
        }

        throw new InvalidOperationException("No non-HQ adjacent pair found on the map.");
    }
}
