using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Setup;
using Risk.Engine.State;

namespace Risk.Tests.Fakes;

/// <summary>
/// Test-only helper that drives a fresh game through the turn-based initial
/// placement loop, so engine tests can start directly from the Reinforce
/// phase without re-implementing the placement loop in every test.
/// </summary>
internal static class GameStateBuilder
{
    public static GameState CompleteSetup(int playerCount, GameMode mode = GameMode.TwoPlayer)
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(playerCount, mode, QueuedDiceRoller.ForRollOff(playerCount)));
        var state = ok.State;
        var engine = new GameEngine(new QueuedDiceRoller());

        if (state.Turn.Phase == TurnPhase.Claim)
        {
            state = CompleteClaimPhase(state, engine);
        }

        while (state.Players.Any(p => p.TroopsRemaining > 0))
        {
            var actor = state.Turn.CurrentPlayer;
            var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new PlaceTroopsCommand(actor, territory, 1)));
            state = result.State;
        }

        return state;
    }

    /// <summary>
    /// Fast-forwards a <see cref="TurnPhase.Claim"/> state through the full
    /// round-robin claim sequence — one unowned territory per
    /// <see cref="ClaimTerritoryCommand"/>, in whatever enumeration order
    /// <see cref="GameState.Territories"/> yields — until every territory is
    /// owned and the engine transitions to <see cref="TurnPhase.Setup"/>. A
    /// no-op if <paramref name="state"/> is not currently in
    /// <see cref="TurnPhase.Claim"/>.
    /// </summary>
    public static GameState CompleteClaimPhase(GameState state, GameEngine engine)
    {
        while (state.Turn.Phase == TurnPhase.Claim)
        {
            var actor = state.Turn.CurrentPlayer;
            var territory = state.Territories.First(kv => kv.Value.Owner is null).Key;
            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new ClaimTerritoryCommand(actor, territory, 1)));
            state = result.State;
        }

        return state;
    }

    /// <summary>
    /// Places all of the current player's Reinforce-phase troops (one at a
    /// time, onto whichever territory they own first in enumeration order)
    /// using <paramref name="engine"/>. Reusable across turns in a scripted
    /// full-game test, unlike <see cref="CompleteSetup"/> which only drives
    /// the very first Setup-to-Reinforce transition.
    /// </summary>
    public static GameState PlaceAllReinforcementTroops(GameState state, GameEngine engine)
    {
        var actor = state.Turn.CurrentPlayer;

        while (state.Players.Single(p => p.Id == actor).TroopsRemaining > 0)
        {
            var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new PlaceTroopsCommand(actor, territory, 1)));
            state = result.State;
        }

        return state;
    }
}
