using Risk.Domain.Cards;
using Risk.Domain.Dice;
using Risk.Domain.Errors;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Combat;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Modes;
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
    private const int MandatoryTradeThreshold = 5;

    /// <summary>
    /// <see cref="GameMode.SecretMission"/>'s <see cref="IVictoryRule"/>.
    /// Static, not constructor-injected, per design D3 — a full mode
    /// resolver is deferred until a second mode is wired.
    /// </summary>
    private static readonly IVictoryRule SecretMissionVictory = new SecretMissionVictoryRule();

    public CommandResult<GameState, GameEvent> Execute(GameState state, GameCommand command)
    {
        if (state.Status is GameStatus.Won)
        {
            return Reject(GameErrorCode.GameOver, "The game has already ended.");
        }

        if (command.Actor != state.Turn.CurrentPlayer)
        {
            return Reject(GameErrorCode.NotYourTurn, "It is not your turn.");
        }

        if (state.Turn.PendingOccupation is not null && command is not OccupyCommand)
        {
            return Reject(GameErrorCode.OccupationPending, "Resolve the pending occupation before issuing other commands.");
        }

        // Mandatory trade-in gate (checked after PendingOccupation so a
        // conquest's occupation is always resolved first, even if the
        // elimination that created it also pushed the actor's hand over the
        // threshold): two distinct rulebook rules, kept separate because a
        // single unconditional `count >= threshold` check cannot distinguish
        // "elimination landed at exactly 5" (never blocks) from "elimination
        // landed at 8, partially traded down to 5" (must keep blocking) —
        // both are `count == 5` but require opposite outcomes. The
        // Reinforce clause only fires at turn start (a hand cannot grow
        // during Reinforce itself — growth only happens via an elimination
        // or the Attack→Fortify conquest draw, both in Attack — so `count`
        // at Reinforce always equals the count carried in at turn start;
        // this breaks if a future mode grants cards mid-Reinforce or
        // re-enters Reinforce mid-turn). The overflow clause is driven by
        // `Turn.MandatoryTradeDown`, armed/cleared in `ExecuteAttack`/
        // `ExecuteTradeCards`. OccupyCommand is exempt for the same reason
        // the PendingOccupation gate above exempts it.
        var actorHand = state.Players.Single(p => p.Id == command.Actor).Hand;
        var mandatoryTradeAtTurnStart = state.Turn.Phase == TurnPhase.Reinforce && actorHand.Count >= MandatoryTradeThreshold;
        var mandatoryTradeOverflow = state.Turn.MandatoryTradeDown && actorHand.Count >= MandatoryTradeThreshold;
        if ((mandatoryTradeAtTurnStart || mandatoryTradeOverflow) && command is not (TradeCardsCommand or OccupyCommand))
        {
            return Reject(GameErrorCode.MandatoryTradeRequired, "You must trade in a valid card set before taking further actions.");
        }

        var requiredPhase = RequiredPhaseFor(command);
        if (requiredPhase is { } phase && state.Turn.Phase != phase)
        {
            return Reject(GameErrorCode.WrongPhase, $"{command.GetType().Name} cannot be issued during {state.Turn.Phase}.");
        }

        return command switch
        {
            PlaceTroopsCommand place => ExecutePlaceTroops(state, place),
            TradeCardsCommand trade => ExecuteTradeCards(state, trade),
            AttackCommand attack => ExecuteAttack(state, attack),
            OccupyCommand occupy => ExecuteOccupy(state, occupy),
            FortifyCommand fortify => ExecuteFortify(state, fortify),
            EndPhaseCommand endPhase => ExecuteEndPhase(state, endPhase),
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
        // Trading is phase-agnostic: it can be a voluntary Reinforce-phase
        // action (bonus troops land in the current/next Reinforce pool) or a
        // mandatory overflow trade-down forced mid-Attack by an elimination
        // (see the mandatory-trade gate in Execute and PR7's resolution of
        // design's open gate-ordering question).
        TradeCardsCommand => null,
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

    private static CommandResult<GameState, GameEvent> ExecuteTradeCards(GameState state, TradeCardsCommand command)
    {
        var player = state.Players.Single(p => p.Id == command.Actor);

        if (!TryRemoveCards(player.Hand, command.Cards, out var remainingHand))
        {
            return Reject(GameErrorCode.InvalidCardSet, "You can only trade cards you hold.");
        }

        if (!CardSet.IsValid(command.Cards))
        {
            return Reject(GameErrorCode.InvalidCardSet, "The selected cards do not form a valid trade-in set.");
        }

        var tradeNumber = state.TradesCompleted + 1;
        var bonus = CardTradeBonus.ForTradeNumber(tradeNumber);
        var updatedPlayer = player with { Hand = remainingHand, TroopsRemaining = player.TroopsRemaining + bonus };

        IReadOnlyList<PlayerState> updatedPlayers = state.Players
            .Select(p => p.Id == updatedPlayer.Id ? updatedPlayer : p)
            .ToArray();

        var events = new List<GameEvent> { new CardsTraded(command.Actor, command.Cards, bonus) };

        // Clear the overflow mandatory-trade flag only once the trade
        // leaves the actor at or below the floor (4 cards): a partial
        // trade-down that still leaves 5+ cards must keep the flag armed so
        // the gate in Execute keeps blocking non-trade commands through a
        // multi-trade overflow sequence.
        var nextTurn = state.Turn.MandatoryTradeDown && remainingHand.Count < MandatoryTradeThreshold
            ? state.Turn with { MandatoryTradeDown = false }
            : state.Turn;

        var newState = state with
        {
            Players = updatedPlayers,
            TradesCompleted = tradeNumber,
            Turn = nextTurn,
            Log = [.. state.Log, .. events]
        };

        return new CommandResult<GameState, GameEvent>.Ok(newState, events);
    }

    /// <summary>
    /// Attempts to remove every card in <paramref name="cards"/> from
    /// <paramref name="hand"/> (by value, one instance per match). Returns
    /// false without mutating anything meaningful if the hand doesn't
    /// contain all of them.
    /// </summary>
    private static bool TryRemoveCards(IReadOnlyList<Card> hand, IReadOnlyList<Card> cards, out IReadOnlyList<Card> remaining)
    {
        var working = new List<Card>(hand);

        foreach (var card in cards)
        {
            if (!working.Remove(card))
            {
                remaining = hand;
                return false;
            }
        }

        remaining = working;
        return true;
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
        var updatedPlayers = state.Players;
        var newStatus = state.Status;

        if (remainingDefenderTroops <= 0)
        {
            updatedTerritories[command.To] = new TerritoryState(command.Actor, 0);
            events.Add(new TerritoryConquered(command.Actor, defenderTerritory.Owner, command.To));
            nextTurn = state.Turn with
            {
                ConqueredThisTurn = true,
                PendingOccupation = new PendingOccupation(command.From, command.To, command.DiceCount)
            };

            var defenderOwnsAnyTerritory = updatedTerritories.Values.Any(t => t.Owner == defenderTerritory.Owner);
            if (!defenderOwnsAnyTerritory)
            {
                updatedPlayers = EliminatePlayer(state.Players, defenderTerritory.Owner, command.Actor, events);

                // Arm the overflow mandatory-trade flag immediately if the
                // transferred cards push the eliminator to 6+ (landing at
                // exactly 5 defers to the eliminator's next Reinforce phase
                // instead — see the invariant comment on TurnState).
                var eliminatorHandCount = updatedPlayers.Single(p => p.Id == command.Actor).Hand.Count;
                if (eliminatorHandCount >= MandatoryTradeThreshold + 1)
                {
                    nextTurn = nextTurn with { MandatoryTradeDown = true };
                }
            }

            if (state.Mode == GameMode.SecretMission)
            {
                var postConquest = state with { Territories = updatedTerritories, Players = updatedPlayers, Turn = nextTurn };
                if (SecretMissionVictory.CheckVictory(postConquest) is { } winner)
                {
                    newStatus = new GameStatus.Won(winner);
                    events.Add(new GameWon(winner));
                }
            }
            else
            {
                // Pre-refactor inline check, byte-identical (Classic / TwoPlayer / Capital).
                var attackerOwnsEveryTerritory = updatedTerritories.Values.Count(t => t.Owner == command.Actor) == WorldMap.Territories.Count;
                if (attackerOwnsEveryTerritory)
                {
                    newStatus = new GameStatus.Won(command.Actor);
                    events.Add(new GameWon(command.Actor));
                }
            }
        }
        else
        {
            updatedTerritories[command.To] = defenderTerritory with { Troops = remainingDefenderTroops };
        }

        var newState = state with
        {
            Territories = updatedTerritories,
            Players = updatedPlayers,
            Turn = nextTurn,
            Status = newStatus,
            Log = [.. state.Log, .. events]
        };

        return new CommandResult<GameState, GameEvent>.Ok(newState, events);
    }

    /// <summary>
    /// Marks <paramref name="victim"/> eliminated and transfers their entire
    /// hand to <paramref name="eliminator"/>. Appends a
    /// <see cref="PlayerEliminated"/> event to <paramref name="events"/>.
    /// </summary>
    private static IReadOnlyList<PlayerState> EliminatePlayer(
        IReadOnlyList<PlayerState> players, PlayerId victim, PlayerId eliminator, List<GameEvent> events)
    {
        var victimPlayer = players.Single(p => p.Id == victim);
        var eliminatorPlayer = players.Single(p => p.Id == eliminator);
        var transferredCards = victimPlayer.Hand;

        var eliminatedVictim = victimPlayer with { IsEliminated = true, Hand = [] };
        var enrichedEliminator = eliminatorPlayer with { Hand = [.. eliminatorPlayer.Hand, .. transferredCards] };

        events.Add(new PlayerEliminated(victim, eliminator, transferredCards.Count));

        return players
            .Select(p => p.Id == eliminatedVictim.Id ? eliminatedVictim
                : p.Id == enrichedEliminator.Id ? enrichedEliminator
                : p)
            .ToArray();
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

    private static CommandResult<GameState, GameEvent> ExecuteFortify(GameState state, FortifyCommand command)
    {
        if (!state.Territories.TryGetValue(command.From, out var sourceTerritory) || sourceTerritory.Owner != command.Actor)
        {
            return Reject(GameErrorCode.NotOwner, "You do not own the source territory.");
        }

        if (!state.Territories.TryGetValue(command.To, out var destinationTerritory) || destinationTerritory.Owner != command.Actor)
        {
            return Reject(GameErrorCode.NotOwner, "You do not own the destination territory.");
        }

        if (state.Turn.FortifyUsed)
        {
            return Reject(GameErrorCode.FortifyAlreadyUsed, "Fortify has already been used this turn.");
        }

        if (command.Troops < 1 || command.Troops > sourceTerritory.Troops - 1)
        {
            return Reject(GameErrorCode.InvalidTroopCount, "Troop count must leave at least one troop behind at the source.");
        }

        if (!ConnectivityRules.HasFriendlyPath(state.Territories, command.Actor, command.From, command.To))
        {
            return Reject(GameErrorCode.NoFriendlyPath, "No chain of your own territories connects these two territories.");
        }

        var updatedTerritories = new Dictionary<TerritoryId, TerritoryState>(state.Territories)
        {
            [command.From] = sourceTerritory with { Troops = sourceTerritory.Troops - command.Troops },
            [command.To] = destinationTerritory with { Troops = destinationTerritory.Troops + command.Troops }
        };

        var events = new List<GameEvent> { new TroopsFortified(command.Actor, command.From, command.To, command.Troops) };

        var newState = state with
        {
            Territories = updatedTerritories,
            Turn = state.Turn with { FortifyUsed = true },
            Log = [.. state.Log, .. events]
        };

        return new CommandResult<GameState, GameEvent>.Ok(newState, events);
    }

    private static CommandResult<GameState, GameEvent> ExecuteEndPhase(GameState state, EndPhaseCommand command)
    {
        if (state.Turn.Phase == TurnPhase.Reinforce)
        {
            var actor = state.Players.Single(p => p.Id == command.Actor);
            if (actor.TroopsRemaining > 0)
            {
                return Reject(
                    GameErrorCode.ReinforcementIncomplete,
                    "You must place all reinforcement troops before ending the Reinforce phase.");
            }
        }

        return state.Turn.Phase switch
        {
            TurnPhase.Reinforce => AdvancePhase(state, TurnPhase.Attack),
            TurnPhase.Attack => AdvanceFromAttackToFortify(state),
            TurnPhase.Fortify => AdvanceToNextPlayer(state),
            _ => Reject(GameErrorCode.WrongPhase, $"Cannot end phase during {state.Turn.Phase}.")
        };
    }

    private static CommandResult<GameState, GameEvent> AdvancePhase(GameState state, TurnPhase nextPhase)
    {
        var events = new List<GameEvent> { new PhaseChanged(state.Turn.Phase, nextPhase, state.Turn.CurrentPlayer) };

        var newState = state with
        {
            Turn = state.Turn with { Phase = nextPhase },
            Log = [.. state.Log, .. events]
        };

        return new CommandResult<GameState, GameEvent>.Ok(newState, events);
    }

    /// <summary>
    /// Ends the current player's Attack phase: awards the acting player a
    /// card if they conquered at least one territory this turn and the deck
    /// still has cards (classic Risk: an empty deck simply means no draw),
    /// emitting <c>CardDrawn</c> before <c>PhaseChanged</c> so the draw is
    /// always announced before the transition to Fortify. Explicitly clears
    /// <c>ConqueredThisTurn</c> at this transition — Fortify's
    /// <c>TurnState</c> is a mutation of the existing one (via <c>with</c>),
    /// not a freshly constructed one, so the flag would otherwise persist
    /// stale through the acting player's entire Fortify phase.
    /// </summary>
    private static CommandResult<GameState, GameEvent> AdvanceFromAttackToFortify(GameState state)
    {
        var actor = state.Turn.CurrentPlayer;
        var events = new List<GameEvent>();
        var deck = state.Deck;
        Card? drawnCard = null;

        if (state.Turn.ConqueredThisTurn && deck.Count > 0)
        {
            drawnCard = deck[0];
            deck = deck.Skip(1).ToArray();
            events.Add(new CardDrawn(actor, drawnCard));
        }

        events.Add(new PhaseChanged(TurnPhase.Attack, TurnPhase.Fortify, actor));

        IReadOnlyList<PlayerState> updatedPlayers = drawnCard is null
            ? state.Players
            : state.Players
                .Select(p => p.Id == actor ? p with { Hand = [.. p.Hand, drawnCard] } : p)
                .ToArray();

        var newState = state with
        {
            Players = updatedPlayers,
            Deck = deck,
            Turn = state.Turn with { Phase = TurnPhase.Fortify, ConqueredThisTurn = false },
            Log = [.. state.Log, .. events]
        };

        return new CommandResult<GameState, GameEvent>.Ok(newState, events);
    }

    /// <summary>
    /// Ends the current player's Fortify phase: rotates to the next player,
    /// resets both per-turn flags (<c>ConqueredThisTurn</c>,
    /// <c>FortifyUsed</c>) for the fresh turn via a newly constructed
    /// <c>TurnState</c>, and assigns that player's Reinforce troop pool.
    /// The conquest card draw no longer happens here — see
    /// <see cref="AdvanceFromAttackToFortify"/> for the Attack → Fortify
    /// transition where it is now awarded.
    /// </summary>
    private static CommandResult<GameState, GameEvent> AdvanceToNextPlayer(GameState state)
    {
        var departingPlayerId = state.Turn.CurrentPlayer;
        var currentIndex = state.Players.ToList().FindIndex(p => p.Id == departingPlayerId);
        var nextPlayer = NextActivePlayer(state.Players, currentIndex);

        var reinforcement = Reinforcement.Calculate(state.Territories, nextPlayer.Id);
        IReadOnlyList<PlayerState> updatedPlayers = state.Players
            .Select(p => p.Id == nextPlayer.Id ? p with { TroopsRemaining = reinforcement } : p)
            .ToArray();

        var events = new List<GameEvent> { new PhaseChanged(TurnPhase.Fortify, TurnPhase.Reinforce, nextPlayer.Id) };

        var newState = state with
        {
            Players = updatedPlayers,
            Turn = new TurnState(nextPlayer.Id, TurnPhase.Reinforce),
            Log = [.. state.Log, .. events]
        };

        return new CommandResult<GameState, GameEvent>.Ok(newState, events);
    }

    /// <summary>
    /// The next non-eliminated player after <paramref name="fromIndex"/>,
    /// wrapping around the player list. Eliminated players are skipped so
    /// the turn cycle never lands on someone with no territories left.
    /// </summary>
    private static PlayerState NextActivePlayer(IReadOnlyList<PlayerState> players, int fromIndex)
    {
        for (var offset = 1; offset <= players.Count; offset++)
        {
            var candidate = players[(fromIndex + offset) % players.Count];
            if (!candidate.IsEliminated)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unreachable: at least one active player must remain once the game is won.");
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
