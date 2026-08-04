using Risk.Domain.Dice;
using Risk.Domain.Errors;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Combat;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Rules;
using Risk.Engine.State;
using Risk.Engine.Views;

namespace Risk.Engine;

/// <summary>
/// The single entry point for mutating and observing game state. Every rule
/// check (turn, phase, ownership, troop counts) runs inside <see cref="Execute"/>;
/// callers never pre-validate.
/// </summary>
public sealed class GameEngine(IDiceRoller dice) : IGameEngine
{
    public CommandResult<GameState, GameEvent> Execute(GameState state, GameCommand command)
    {
        if (command.Actor != state.Turn.CurrentPlayer)
        {
            return Reject(GameErrorCode.NotYourTurn, "It is not your turn.");
        }

        if (state.Turn.PendingOccupation is not null && command is not OccupyCommand)
        {
            return Reject(GameErrorCode.OccupationPending, "Resolve the pending occupation before issuing other commands.");
        }

        var requiredPhase = RequiredPhaseFor(command);
        if (requiredPhase is { } phase && state.Turn.Phase != phase)
        {
            return Reject(GameErrorCode.WrongPhase, $"{command.GetType().Name} cannot be issued during {state.Turn.Phase}.");
        }

        return command switch
        {
            PlaceTroopsCommand place => ExecutePlaceTroops(state, place),
            AttackCommand attack => ExecuteAttack(state, attack),
            OccupyCommand occupy => ExecuteOccupy(state, occupy),
            FortifyCommand or TradeCardsCommand or EndPhaseCommand =>
                throw new NotImplementedException($"{command.GetType().Name} handling arrives in a later PR."),
            _ => throw new InvalidOperationException("Unreachable: unknown GameCommand type.")
        };
    }

    public PlayerView Observe(GameState state, PlayerId viewer)
    {
        var ownHand = state.Players.Single(p => p.Id == viewer).Hand;
        var otherCounts = state.Players
            .Where(p => p.Id != viewer)
            .ToDictionary(p => p.Id, p => p.Hand.Count);

        return new PlayerView(state.Territories, ownHand, otherCounts, state.Turn);
    }

    private static TurnPhase? RequiredPhaseFor(GameCommand command) => command switch
    {
        PlaceTroopsCommand => null,
        TradeCardsCommand => TurnPhase.Reinforce,
        AttackCommand => TurnPhase.Attack,
        OccupyCommand => TurnPhase.Attack,
        FortifyCommand => TurnPhase.Fortify,
        EndPhaseCommand => null,
        _ => throw new InvalidOperationException("Unreachable: unknown GameCommand type.")
    };

    private static CommandResult<GameState, GameEvent> ExecutePlaceTroops(GameState state, PlaceTroopsCommand command)
    {
        if (state.Turn.Phase is not (TurnPhase.Setup or TurnPhase.Reinforce))
        {
            return Reject(GameErrorCode.WrongPhase, "Troops can only be placed during setup or reinforcement.");
        }

        if (!state.Territories.TryGetValue(command.Territory, out var territory) || territory.Owner != command.Actor)
        {
            return Reject(GameErrorCode.NotOwner, "You do not own that territory.");
        }

        var player = state.Players.Single(p => p.Id == command.Actor);

        if (command.Troops < 1 || command.Troops > player.TroopsRemaining)
        {
            return Reject(GameErrorCode.InvalidTroopCount, "Troop count must be between 1 and the troops you have remaining.");
        }

        if (state.Turn.Phase == TurnPhase.Setup && command.Troops != 1)
        {
            return Reject(GameErrorCode.InvalidTroopCount, "During setup, exactly one troop is placed per turn.");
        }

        var updatedTerritories = new Dictionary<TerritoryId, TerritoryState>(state.Territories)
        {
            [command.Territory] = territory with { Troops = territory.Troops + command.Troops }
        };

        var updatedPlayer = player with { TroopsRemaining = player.TroopsRemaining - command.Troops };
        IReadOnlyList<PlayerState> updatedPlayers = state.Players
            .Select(p => p.Id == updatedPlayer.Id ? updatedPlayer : p)
            .ToArray();

        var events = new List<GameEvent> { new TroopsPlaced(command.Actor, command.Territory, command.Troops) };
        var nextTurn = state.Turn;

        if (state.Turn.Phase == TurnPhase.Setup)
        {
            (nextTurn, updatedPlayers) = AdvanceAfterSetupPlacement(state.Turn, updatedPlayers, updatedTerritories, events);
        }

        var newState = state with
        {
            Territories = updatedTerritories,
            Players = updatedPlayers,
            Turn = nextTurn,
            Log = [.. state.Log, .. events]
        };

        return new CommandResult<GameState, GameEvent>.Ok(newState, events);
    }

    private CommandResult<GameState, GameEvent> ExecuteAttack(GameState state, AttackCommand command)
    {
        if (!state.Territories.TryGetValue(command.From, out var attackerTerritory) || attackerTerritory.Owner != command.Actor)
        {
            return Reject(GameErrorCode.NotOwner, "You do not own the attacking territory.");
        }

        if (!state.Territories.TryGetValue(command.To, out var defenderTerritory) || defenderTerritory.Owner == command.Actor)
        {
            return Reject(GameErrorCode.NotOwner, "The target territory must be owned by another player.");
        }

        if (!WorldMap.AreAdjacent(command.From, command.To))
        {
            return Reject(GameErrorCode.NotAdjacent, "Territories are not adjacent.");
        }

        if (command.DiceCount is < 1 or > 3)
        {
            return Reject(GameErrorCode.InvalidDiceCount, "Dice count must be between 1 and 3.");
        }

        if (command.DiceCount > attackerTerritory.Troops - 1)
        {
            return Reject(GameErrorCode.InsufficientTroops, "Rolling that many dice would leave no troops behind.");
        }

        var defenderDiceCount = Math.Min(2, defenderTerritory.Troops);
        var attackerRolls = dice.Roll(command.DiceCount);
        var defenderRolls = dice.Roll(defenderDiceCount);
        var outcome = BattleResolver.Resolve(attackerRolls, defenderRolls);

        var updatedAttackerTerritory = attackerTerritory with { Troops = attackerTerritory.Troops - outcome.AttackerLosses };
        var remainingDefenderTroops = defenderTerritory.Troops - outcome.DefenderLosses;

        var events = new List<GameEvent>
        {
            new BattleResolved(command.Actor, command.From, command.To, outcome.AttackerRolls, outcome.DefenderRolls, outcome.AttackerLosses, outcome.DefenderLosses)
        };

        var updatedTerritories = new Dictionary<TerritoryId, TerritoryState>(state.Territories)
        {
            [command.From] = updatedAttackerTerritory
        };

        var nextTurn = state.Turn;

        if (remainingDefenderTroops <= 0)
        {
            updatedTerritories[command.To] = new TerritoryState(command.Actor, 0);
            events.Add(new TerritoryConquered(command.Actor, defenderTerritory.Owner, command.To));
            nextTurn = state.Turn with
            {
                ConqueredThisTurn = true,
                PendingOccupation = new PendingOccupation(command.From, command.To, command.DiceCount)
            };
        }
        else
        {
            updatedTerritories[command.To] = defenderTerritory with { Troops = remainingDefenderTroops };
        }

        var newState = state with
        {
            Territories = updatedTerritories,
            Turn = nextTurn,
            Log = [.. state.Log, .. events]
        };

        return new CommandResult<GameState, GameEvent>.Ok(newState, events);
    }

    private static CommandResult<GameState, GameEvent> ExecuteOccupy(GameState state, OccupyCommand command)
    {
        var pending = state.Turn.PendingOccupation;
        if (pending is null)
        {
            return Reject(GameErrorCode.NoPendingOccupation, "There is no conquered territory awaiting occupation.");
        }

        var sourceTerritory = state.Territories[pending.From];
        var maxMovable = sourceTerritory.Troops - 1;

        if (command.Troops < pending.MinimumTroops || command.Troops > maxMovable)
        {
            return Reject(
                GameErrorCode.InvalidTroopCount,
                $"Occupation troop count must be between {pending.MinimumTroops} and {maxMovable}.");
        }

        var updatedSourceTerritory = sourceTerritory with { Troops = sourceTerritory.Troops - command.Troops };
        var updatedConquered = state.Territories[pending.Conquered] with { Troops = command.Troops };

        var updatedTerritories = new Dictionary<TerritoryId, TerritoryState>(state.Territories)
        {
            [pending.From] = updatedSourceTerritory,
            [pending.Conquered] = updatedConquered
        };

        var events = new List<GameEvent> { new TerritoryOccupied(command.Actor, pending.Conquered, command.Troops) };

        var newState = state with
        {
            Territories = updatedTerritories,
            Turn = state.Turn with { PendingOccupation = null },
            Log = [.. state.Log, .. events]
        };

        return new CommandResult<GameState, GameEvent>.Ok(newState, events);
    }

    private static (TurnState Turn, IReadOnlyList<PlayerState> Players) AdvanceAfterSetupPlacement(
        TurnState turn,
        IReadOnlyList<PlayerState> players,
        IReadOnlyDictionary<TerritoryId, TerritoryState> territories,
        List<GameEvent> events)
    {
        var currentIndex = players.ToList().FindIndex(p => p.Id == turn.CurrentPlayer);

        for (var offset = 1; offset <= players.Count; offset++)
        {
            var candidate = players[(currentIndex + offset) % players.Count];
            if (candidate.TroopsRemaining > 0)
            {
                return (turn with { CurrentPlayer = candidate.Id }, players);
            }
        }

        // Every player has placed all starting troops: setup is complete and
        // the first player begins the normal turn cycle in Reinforce.
        var firstPlayer = players[0];
        var reinforcement = Reinforcement.Calculate(territories, firstPlayer.Id);
        IReadOnlyList<PlayerState> reinforcedPlayers = players
            .Select(p => p.Id == firstPlayer.Id ? p with { TroopsRemaining = reinforcement } : p)
            .ToArray();

        events.Add(new PhaseChanged(TurnPhase.Setup, TurnPhase.Reinforce, firstPlayer.Id));

        return (new TurnState(firstPlayer.Id, TurnPhase.Reinforce), reinforcedPlayers);
    }

    private static CommandResult<GameState, GameEvent> Reject(GameErrorCode code, string message) =>
        new CommandResult<GameState, GameEvent>.Rejected(new GameError(code, message));
}
