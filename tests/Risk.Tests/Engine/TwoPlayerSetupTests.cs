using Risk.Domain.Errors;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Setup;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

/// <summary>
/// PR1 of roadmap item 4.1: <see cref="GameMode.TwoPlayer"/>'s 3-way
/// territory deal (P1 / P2 / Neutral) via <c>TwoPlayerSetupStrategy</c>,
/// wired through <see cref="GameSetup.Create"/>. Setup completion (Phase A
/// budget generalization, Phase B neutral placement) is PR2/PR3's scope —
/// these tests only cover the initial dealt <see cref="GameState"/>.
/// </summary>
public class TwoPlayerSetupTests
{
    private static (GameState State, GameEngine Engine) StartPhaseA()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(2, GameMode.TwoPlayer, QueuedDiceRoller.ForRollOff(2)));
        return (ok.State, new GameEngine(new QueuedDiceRoller()));
    }

    [Fact]
    public void Create_deals_42_territories_into_three_14_piles_with_one_neutral()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(2, GameMode.TwoPlayer, QueuedDiceRoller.ForRollOff(2)));
        var state = ok.State;

        Assert.Equal(3, state.Players.Count);

        var neutrals = state.Players.Where(p => p.IsNeutral).ToArray();
        Assert.Single(neutrals);
        Assert.Equal(new PlayerId(2), neutrals[0].Id);
        Assert.False(state.Players[0].IsNeutral);
        Assert.False(state.Players[1].IsNeutral);

        var counts = state.Territories.Values
            .GroupBy(t => t.Owner!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(3, counts.Count);
        Assert.Equal(42, counts.Values.Sum());
        Assert.All(counts.Values, count => Assert.Equal(14, count));
        Assert.All(state.Territories.Values, t => Assert.Equal(1, t.Troops));

        Assert.All(state.Players, p => Assert.Equal(26, p.TroopsRemaining));

        Assert.Equal(TurnPhase.Setup, state.Turn.Phase);
        Assert.Equal(state.Players[0].Id, state.Turn.CurrentPlayer);
    }

    [Fact]
    public void Create_assigns_120_total_troops_for_TwoPlayer_via_three_way_deal()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(2, GameMode.TwoPlayer, QueuedDiceRoller.ForRollOff(2)));
        var state = ok.State;

        var totalRemaining = state.Players.Sum(p => p.TroopsRemaining);
        var territoriesPlaced = state.Territories.Count; // 1 troop auto-placed per dealt territory

        Assert.Equal(3 * 40, totalRemaining + territoriesPlaced);
    }

    // PR2 (design D1): Phase A's per-turn budget is derived from
    // TroopsRemaining's parity, not a new counter — see
    // GameEngine.SetupTroopsPerTurn/SetupBudgetRemaining.

    [Fact]
    public void Placing_two_troops_on_one_territory_advances_the_turn()
    {
        var (state, engine) = StartPhaseA();
        var actor = state.Turn.CurrentPlayer;
        var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;

        var result = engine.Execute(state, new PlaceTroopsCommand(actor, territory, 2));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(24, ok.State.Players.Single(p => p.Id == actor).TroopsRemaining);
        Assert.Equal(3, ok.State.Territories[territory].Troops);
        Assert.NotEqual(actor, ok.State.Turn.CurrentPlayer);
        Assert.False(ok.State.Players.Single(p => p.Id == ok.State.Turn.CurrentPlayer).IsNeutral);
    }

    [Fact]
    public void Splitting_one_and_one_across_two_territories_keeps_the_turn_then_advances_on_the_second_placement()
    {
        var (state, engine) = StartPhaseA();
        var actor = state.Turn.CurrentPlayer;
        var ownedTerritories = state.Territories.Where(kv => kv.Value.Owner == actor).Select(kv => kv.Key).ToArray();
        var territoryA = ownedTerritories[0];
        var territoryB = ownedTerritories[1];

        var firstResult = engine.Execute(state, new PlaceTroopsCommand(actor, territoryA, 1));
        var firstOk = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(firstResult);

        Assert.Equal(25, firstOk.State.Players.Single(p => p.Id == actor).TroopsRemaining);
        Assert.Equal(actor, firstOk.State.Turn.CurrentPlayer); // mid-turn: budget not exhausted

        var secondResult = engine.Execute(firstOk.State, new PlaceTroopsCommand(actor, territoryB, 1));
        var secondOk = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(secondResult);

        Assert.Equal(24, secondOk.State.Players.Single(p => p.Id == actor).TroopsRemaining);
        Assert.Equal(2, secondOk.State.Territories[territoryA].Troops);
        Assert.Equal(2, secondOk.State.Territories[territoryB].Troops);
        Assert.NotEqual(actor, secondOk.State.Turn.CurrentPlayer);
    }

    [Fact]
    public void Troops_three_is_rejected_in_TwoPlayer_Phase_A()
    {
        var (state, engine) = StartPhaseA();
        var actor = state.Turn.CurrentPlayer;
        var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;

        var result = engine.Execute(state, new PlaceTroopsCommand(actor, territory, 3));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidTroopCount, rejection.Error.Code);
    }

    [Fact]
    public void Troops_two_is_rejected_after_a_partial_one_troop_placement()
    {
        var (state, engine) = StartPhaseA();
        var actor = state.Turn.CurrentPlayer;
        var ownedTerritories = state.Territories.Where(kv => kv.Value.Owner == actor).Select(kv => kv.Key).ToArray();
        var territoryA = ownedTerritories[0];
        var territoryB = ownedTerritories[1];

        var firstResult = engine.Execute(state, new PlaceTroopsCommand(actor, territoryA, 1));
        var firstOk = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(firstResult);

        // Only 1 troop remains of this turn's 2-troop budget; requesting 2
        // more must be rejected even though the actor still has 25 troops
        // left in their overall pool.
        var secondResult = engine.Execute(firstOk.State, new PlaceTroopsCommand(actor, territoryB, 2));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(secondResult);
        Assert.Equal(GameErrorCode.InvalidTroopCount, rejection.Error.Code);
    }

    [Fact]
    public void TradeCards_cannot_alter_TroopsRemaining_during_TwoPlayer_Setup()
    {
        var (state, engine) = StartPhaseA();
        var actor = state.Turn.CurrentPlayer;
        var actorBefore = state.Players.Single(p => p.Id == actor);

        // A 3-card hand is required for CardSet.IsValid, but Setup-phase
        // players start with an empty hand, so any trade attempt during
        // Setup is rejected before it could ever touch TroopsRemaining —
        // pinning design D1's "pool cannot grow during Setup" invariant.
        var result = engine.Execute(state, new TradeCardsCommand(actor, actorBefore.Hand));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidCardSet, rejection.Error.Code);
    }

    [Fact]
    public void EndPhase_is_rejected_during_TwoPlayer_Setup()
    {
        var (state, engine) = StartPhaseA();
        var actor = state.Turn.CurrentPlayer;

        var result = engine.Execute(state, new EndPhaseCommand(actor));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.WrongPhase, rejection.Error.Code);
    }

    // PR3 (design D3/D4): Phase B opens only once both humans' Setup pools
    // hit 0 (derived, no flag — GameEngine.IsPhaseB). Drains Phase A with the
    // 2-on-one budget so every Phase B test starts from a real Phase B state.
    private static (GameState State, GameEngine Engine) StartPhaseB()
    {
        var (state, engine) = StartPhaseA();

        while (state.Players.Where(p => !p.IsNeutral).Any(p => p.TroopsRemaining > 0))
        {
            var actor = state.Turn.CurrentPlayer;
            var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new PlaceTroopsCommand(actor, territory, 2)));
            state = result.State;
        }

        return (state, engine);
    }

    [Fact]
    public void PlaceNeutralTroopsCommand_places_one_troop_on_a_neutral_territory_and_rotates()
    {
        var (state, engine) = StartPhaseB();
        var actor = state.Turn.CurrentPlayer;
        var neutral = state.Players.Single(p => p.IsNeutral);
        var territory = state.Territories.First(kv => kv.Value.Owner == neutral.Id).Key;

        var result = engine.Execute(state, new PlaceNeutralTroopsCommand(actor, territory, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Equal(2, ok.State.Territories[territory].Troops);
        Assert.Equal(25, ok.State.Players.Single(p => p.IsNeutral).TroopsRemaining);
        Assert.NotEqual(actor, ok.State.Turn.CurrentPlayer);
        Assert.False(ok.State.Players.Single(p => p.Id == ok.State.Turn.CurrentPlayer).IsNeutral);
        Assert.Contains(ok.Events, e => e is NeutralTroopsPlaced placed
            && placed.Placer == actor && placed.Territory == territory && placed.Troops == 1);
    }

    [Fact]
    public void PlaceNeutralTroopsCommand_rejects_neutral_as_actor()
    {
        var (state, engine) = StartPhaseB();
        var neutral = state.Players.Single(p => p.IsNeutral);
        var territory = state.Territories.First(kv => kv.Value.Owner == neutral.Id).Key;

        var result = engine.Execute(state, new PlaceNeutralTroopsCommand(neutral.Id, territory, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotYourTurn, rejection.Error.Code);
    }

    [Fact]
    public void PlaceNeutralTroopsCommand_rejects_a_target_owned_by_self_or_opponent()
    {
        var (state, engine) = StartPhaseB();
        var actor = state.Turn.CurrentPlayer;
        var opponent = state.Players.Single(p => !p.IsNeutral && p.Id != actor).Id;

        var ownTerritory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
        var ownResult = engine.Execute(state, new PlaceNeutralTroopsCommand(actor, ownTerritory, 1));
        var ownRejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(ownResult);
        Assert.Equal(GameErrorCode.NotOwner, ownRejection.Error.Code);

        var opponentTerritory = state.Territories.First(kv => kv.Value.Owner == opponent).Key;
        var opponentResult = engine.Execute(state, new PlaceNeutralTroopsCommand(actor, opponentTerritory, 1));
        var opponentRejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(opponentResult);
        Assert.Equal(GameErrorCode.NotOwner, opponentRejection.Error.Code);
    }

    [Fact]
    public void PlaceNeutralTroopsCommand_rejects_during_Phase_A()
    {
        var (state, engine) = StartPhaseA();
        var actor = state.Turn.CurrentPlayer;
        var neutral = state.Players.Single(p => p.IsNeutral);
        var territory = state.Territories.First(kv => kv.Value.Owner == neutral.Id).Key;

        var result = engine.Execute(state, new PlaceNeutralTroopsCommand(actor, territory, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.WrongPhase, rejection.Error.Code);
    }

    [Fact]
    public void PlaceNeutralTroopsCommand_rejects_outside_TwoPlayer_Setup()
    {
        var state = GameStateBuilder.CompleteSetup(4, GameMode.Classic);
        var engine = new GameEngine(new QueuedDiceRoller());
        var actor = state.Turn.CurrentPlayer;
        var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;

        var result = engine.Execute(state, new PlaceNeutralTroopsCommand(actor, territory, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.WrongPhase, rejection.Error.Code);
    }

    /// <summary>
    /// Design D3: <c>GameCommand</c> is a plain <c>abstract record</c>, not a
    /// closed hierarchy — both of <c>GameEngine</c>'s switches fall back to
    /// <c>_ =&gt; throw</c>. Nothing forces <c>PlaceNeutralTroopsCommand</c> to
    /// actually be wired into <c>Execute</c>'s dispatch switch or
    /// <c>RequiredPhaseFor</c>; this test proves both paths are reachable
    /// rather than trusting the compiler to catch a missing case.
    /// </summary>
    [Fact]
    public void PlaceNeutralTroopsCommand_is_reachable_via_Execute_and_RequiredPhaseFor()
    {
        // RequiredPhaseFor wiring: submitted outside Setup, rejected with
        // WrongPhase (not an unreachable-switch exception) — proves
        // RequiredPhaseFor has a real case for this command type.
        var reinforceState = GameStateBuilder.CompleteSetup(4, GameMode.Classic);
        var reinforceEngine = new GameEngine(new QueuedDiceRoller());
        var reinforceActor = reinforceState.Turn.CurrentPlayer;
        var reinforceTerritory = reinforceState.Territories.First(kv => kv.Value.Owner == reinforceActor).Key;
        var wrongPhaseResult = reinforceEngine.Execute(
            reinforceState, new PlaceNeutralTroopsCommand(reinforceActor, reinforceTerritory, 1));
        Assert.Equal(
            GameErrorCode.WrongPhase,
            Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(wrongPhaseResult).Error.Code);

        // Execute dispatch wiring: a valid Phase B command actually resolves
        // (not an unreachable-switch exception) — proves Execute's command
        // switch has a real case for this command type.
        var (phaseBState, phaseBEngine) = StartPhaseB();
        var phaseBActor = phaseBState.Turn.CurrentPlayer;
        var neutral = phaseBState.Players.Single(p => p.IsNeutral);
        var neutralTerritory = phaseBState.Territories.First(kv => kv.Value.Owner == neutral.Id).Key;
        var dispatchResult = phaseBEngine.Execute(
            phaseBState, new PlaceNeutralTroopsCommand(phaseBActor, neutralTerritory, 1));
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(dispatchResult);
    }

    [Fact]
    public void A_full_TwoPlayer_setup_lifecycle_reaches_Reinforce_with_troop_conservation()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(2, GameMode.TwoPlayer, QueuedDiceRoller.ForRollOff(2)));
        var state = ok.State;
        var engine = new GameEngine(new QueuedDiceRoller());

        // Phase A: alternate humans, mixing 2-on-one and 1+1-split turns,
        // never letting the neutral become CurrentPlayer.
        var mixTurn = 0;
        while (state.Players.Where(p => !p.IsNeutral).Any(p => p.TroopsRemaining > 0))
        {
            var actor = state.Turn.CurrentPlayer;
            Assert.False(state.Players.Single(p => p.Id == actor).IsNeutral);

            if (mixTurn % 2 == 0)
            {
                var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
                state = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                    engine.Execute(state, new PlaceTroopsCommand(actor, territory, 2))).State;
            }
            else
            {
                var owned = state.Territories.Where(kv => kv.Value.Owner == actor).Select(kv => kv.Key).ToArray();
                state = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                    engine.Execute(state, new PlaceTroopsCommand(actor, owned[0], 1))).State;
                state = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                    engine.Execute(state, new PlaceTroopsCommand(actor, owned[1], 1))).State;
            }

            mixTurn++;
        }

        Assert.Equal(TurnPhase.Setup, state.Turn.Phase);
        Assert.All(state.Players.Where(p => !p.IsNeutral), p => Assert.Equal(0, p.TroopsRemaining));
        Assert.Equal(26, state.Players.Single(p => p.IsNeutral).TroopsRemaining);

        // Phase B: alternate humans, 1 neutral troop per turn each, until the
        // neutral's pool is fully drained.
        while (state.Players.Single(p => p.IsNeutral).TroopsRemaining > 0)
        {
            var actor = state.Turn.CurrentPlayer;
            Assert.False(state.Players.Single(p => p.Id == actor).IsNeutral);

            var neutralId = state.Players.Single(p => p.IsNeutral).Id;
            var territory = state.Territories.First(kv => kv.Value.Owner == neutralId).Key;
            state = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new PlaceNeutralTroopsCommand(actor, territory, 1))).State;
        }

        // Setup->Reinforce handoff: correct human as CurrentPlayer, no dead
        // end, total troop conservation across the board.
        Assert.Equal(TurnPhase.Reinforce, state.Turn.Phase);
        Assert.Equal(new PlayerId(0), state.Turn.CurrentPlayer);
        Assert.False(state.Players.Single(p => p.Id == state.Turn.CurrentPlayer).IsNeutral);

        var neutralFinal = state.Players.Single(p => p.IsNeutral);
        Assert.Equal(0, neutralFinal.TroopsRemaining);

        var otherHuman = state.Players.Single(p => !p.IsNeutral && p.Id != state.Turn.CurrentPlayer);
        Assert.Equal(0, otherHuman.TroopsRemaining);

        // The current player's freshly granted Reinforce pool is a NEW pool
        // (Reinforcement.Calculate), not a leftover from Setup — it must be
        // positive, not part of the 120-troop Setup allotment below.
        var currentHuman = state.Players.Single(p => p.Id == state.Turn.CurrentPlayer);
        Assert.True(currentHuman.TroopsRemaining > 0);

        var totalOnBoard = state.Territories.Values.Sum(t => t.Troops);
        Assert.Equal(3 * 40, totalOnBoard);
    }
}
