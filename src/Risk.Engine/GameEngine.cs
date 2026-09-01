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
public sealed class GameEngine : IGameEngine
{
    private const int MandatoryTradeThreshold = 5;

    /// <summary>
    /// <see cref="GameMode.SecretMission"/>'s <see cref="IVictoryRule"/>.
    /// Static, not constructor-injected, per design D3 — a full mode
    /// resolver is deferred until a second mode is wired.
    /// </summary>
    private static readonly IVictoryRule SecretMissionVictory = new SecretMissionVictoryRule();

    private readonly IDiceRoller dice;
    private readonly Func<GameMode, IVictoryRule?> victoryRuleFor;

    public GameEngine(IDiceRoller dice) : this(dice, VictoryRules.For)
    {
    }

    /// <summary>
    /// Test-only seam (item 2.1, design D5): lets <c>Risk.Tests</c> inject an
    /// instrumented <see cref="IVictoryRule"/> resolver to positively prove
    /// which <see cref="GameMode"/>s actually route through
    /// <see cref="ExecuteAttack"/>'s <see cref="IVictoryRule"/> dispatch,
    /// instead of inferring it from assertions a byte-identical inline
    /// fallback could also satisfy. The public constructor always delegates
    /// here with the real <see cref="VictoryRules.For"/> resolver, so
    /// production/DI callers (<c>GameEngine(IDiceRoller)</c>) see no
    /// behavior change.
    /// </summary>
    internal GameEngine(IDiceRoller dice, Func<GameMode, IVictoryRule?> victoryRuleFor)
    {
        this.dice = dice;
        this.victoryRuleFor = victoryRuleFor;
    }

    public CommandResult<GameState, GameEvent> Execute(GameState state, GameCommand command)
    {
        if (state.Status is GameStatus.Won)
        {
            return Reject(GameErrorCode.GameOver, "The game has already ended.");
        }

        // Item 4.2/D1: checked before the actor-is-current-player check
        // below. The neutral (TwoPlayer's third army) is not "out of turn"
        // — it can never legitimately act at all, so the diagnostic must say
        // so precisely rather than falling through to NotYourTurn. Uses a
        // non-throwing existence check (not Single) because actor validity
        // is not yet established at this point in the pipeline.
        if (state.Players.Any(p => p.Id == command.Actor && p.IsNeutral))
        {
            return Reject(GameErrorCode.ActorIsNeutral, "The neutral army cannot issue commands.");
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
            PlaceNeutralTroopsCommand placeNeutral => ExecutePlaceNeutralTroops(state, placeNeutral),
            TradeCardsCommand trade => ExecuteTradeCards(state, trade),
            ClaimTerritoryCommand claim => ExecuteClaimTerritory(state, claim),
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
        // Only legal during Setup; the finer-grained "is this actually
        // Phase B" condition is derived state, not a simple phase check, so
        // it is enforced inside ExecutePlaceNeutralTroops (design D3) rather
        // than here.
        PlaceNeutralTroopsCommand => TurnPhase.Setup,
        // Trading is phase-agnostic: it can be a voluntary Reinforce-phase
        // action (bonus troops land in the current/next Reinforce pool) or a
        // mandatory overflow trade-down forced mid-Attack by an elimination
        // (see the mandatory-trade gate in Execute and PR7's resolution of
        // design's open gate-ordering question).
        TradeCardsCommand => null,
        ClaimTerritoryCommand => TurnPhase.Claim,
        AttackCommand => TurnPhase.Attack,
        OccupyCommand => TurnPhase.Attack,
        FortifyCommand => TurnPhase.Fortify,
        EndPhaseCommand => null,
        _ => throw new InvalidOperationException("Unreachable: unknown GameCommand type.")
    };

    /// <summary>
    /// How many troops a player may place per Setup-phase command/turn
    /// (design D1). Every mode except <see cref="GameMode.TwoPlayer"/> keeps
    /// the classic "exactly one troop, immediate rotation" rule (<c>1</c>);
    /// <see cref="GameMode.TwoPlayer"/>'s Phase A allows <c>2</c>, splittable
    /// across one or two commands (reglasrisk.md: "coloca dos tropas sobre
    /// un territorio o dos de los que ocupes"). This is the ONLY place
    /// TwoPlayer's "2 per turn" rule is expressed.
    /// </summary>
    private static int SetupTroopsPerTurn(GameMode mode) => mode == GameMode.TwoPlayer ? 2 : 1;

    /// <summary>
    /// How much of the current turn's Setup placement budget is still
    /// available, derived from <paramref name="troopsRemaining"/>'s parity
    /// against <paramref name="perTurn"/> rather than a separately tracked
    /// counter (design D1 — avoids a stale-flag reset bug class, the same
    /// one already documented on <c>ConqueredThisTurn</c>). A pool that is an
    /// exact multiple of <paramref name="perTurn"/> is always at a turn
    /// boundary, so the full budget is available; otherwise the remainder is
    /// what is left of the turn already in progress. Safe because
    /// <c>TroopsRemaining</c> cannot grow during Setup: <c>EndPhaseCommand</c>
    /// rejects with <see cref="GameErrorCode.WrongPhase"/>, and
    /// <c>TradeCardsCommand</c> — though phase-agnostic — dies on
    /// <c>CardSet.IsValid</c> rejecting any hand size other than 3 (Setup
    /// hands are always empty).
    /// </summary>
    private static int SetupBudgetRemaining(int troopsRemaining, int perTurn) =>
        troopsRemaining % perTurn is 0 ? perTurn : troopsRemaining % perTurn;

    /// <summary>
    /// Design D3: whether <paramref name="state"/> is currently in
    /// <see cref="GameMode.TwoPlayer"/>'s Setup Phase B — derived from state,
    /// not a flag. True once both real humans have exhausted their own
    /// Setup pool (Phase A complete) while the neutral still has troops of
    /// its own left to place. Short-circuits on <c>Mode == TwoPlayer</c>
    /// first so non-TwoPlayer modes (which have no neutral player at all)
    /// never evaluate the neutral-lookup that follows.
    /// </summary>
    private static bool IsPhaseB(GameState state) =>
        state.Mode == GameMode.TwoPlayer
        && state.Turn.Phase == TurnPhase.Setup
        && state.Players.Where(p => !p.IsNeutral).All(p => p.TroopsRemaining == 0)
        && state.Players.Single(p => p.IsNeutral).TroopsRemaining > 0;

    /// <summary>
    /// Handles <see cref="PlaceNeutralTroopsCommand"/> (design D3): a human
    /// chooses where one of the neutral player's troops lands during
    /// <see cref="GameMode.TwoPlayer"/>'s Setup Phase B. Mirrors
    /// <see cref="ExecutePlaceTroops"/>'s territory/pool accounting, but
    /// spends the *neutral's* pool, not the acting human's — the human is
    /// only choosing a target, never receiving the troop.
    /// </summary>
    private static CommandResult<GameState, GameEvent> ExecutePlaceNeutralTroops(GameState state, PlaceNeutralTroopsCommand command)
    {
        if (!IsPhaseB(state))
        {
            return Reject(GameErrorCode.WrongPhase, "Neutral troops can only be placed after both players have placed all of their own setup troops.");
        }

        var neutral = state.Players.Single(p => p.IsNeutral);

        if (!state.Territories.TryGetValue(command.Territory, out var territory) || territory.Owner != neutral.Id)
        {
            return Reject(GameErrorCode.NotOwner, "That territory is not owned by the neutral player.");
        }

        if (command.Troops != 1 || command.Troops > neutral.TroopsRemaining)
        {
            return Reject(GameErrorCode.InvalidTroopCount, "Exactly one neutral troop is placed per turn during Phase B.");
        }

        var updatedTerritories = new Dictionary<TerritoryId, TerritoryState>(state.Territories)
        {
            [command.Territory] = territory with { Troops = territory.Troops + command.Troops }
        };

        var updatedNeutral = neutral with { TroopsRemaining = neutral.TroopsRemaining - command.Troops };
        IReadOnlyList<PlayerState> updatedPlayers = state.Players
            .Select(p => p.Id == updatedNeutral.Id ? updatedNeutral : p)
            .ToArray();

        var events = new List<GameEvent> { new NeutralTroopsPlaced(command.Actor, command.Territory, command.Troops) };
        var (nextTurn, finalPlayers) = AdvanceAfterSetupPlacement(state.Turn, updatedPlayers, updatedTerritories, events);

        var newState = state with
        {
            Territories = updatedTerritories,
            Players = finalPlayers,
            Turn = nextTurn,
            Log = [.. state.Log, .. events]
        };

        return new CommandResult<GameState, GameEvent>.Ok(newState, events);
    }

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

        if (state.Turn.Phase == TurnPhase.Setup
            && command.Troops > SetupBudgetRemaining(player.TroopsRemaining, SetupTroopsPerTurn(state.Mode)))
        {
            return Reject(GameErrorCode.InvalidTroopCount, "Troop count exceeds this turn's setup placement budget.");
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

        if (state.Turn.Phase == TurnPhase.Setup
            && updatedPlayer.TroopsRemaining % SetupTroopsPerTurn(state.Mode) == 0)
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
    /// Claims a previously unowned territory during <see cref="TurnPhase.Claim"/>.
    /// Mirrors <see cref="ExecutePlaceTroops"/>'s troop-pool accounting
    /// (design D3), enforces exactly one troop per claim (design D2 — closes
    /// a deadlock where a player could otherwise exhaust their entire troop
    /// pool on a single claim and be left with no legal command on their
    /// next Claim-phase turn), and — reversing item 1.3's design D4 —
    /// advances <c>Turn</c> via <see cref="AdvanceAfterClaim"/>: round-robin
    /// rotation while territories remain unclaimed, or a
    /// <see cref="TurnPhase.Claim"/> → <see cref="TurnPhase.Setup"/>
    /// transition (at the rotated next player, not a reset to
    /// <c>players[0]</c>) once the map is full.
    /// </summary>
    private static CommandResult<GameState, GameEvent> ExecuteClaimTerritory(GameState state, ClaimTerritoryCommand command)
    {
        if (!state.Territories.TryGetValue(command.Territory, out var territory))
        {
            return Reject(GameErrorCode.NotOwner, "That territory does not exist.");
        }

        if (territory.Owner is not null)
        {
            return Reject(GameErrorCode.NotOwner, "That territory has already been claimed.");
        }

        var player = state.Players.Single(p => p.Id == command.Actor);

        if (command.Troops < 1 || command.Troops > player.TroopsRemaining)
        {
            return Reject(GameErrorCode.InvalidTroopCount, "Troop count must be between 1 and the troops you have remaining.");
        }

        if (state.Turn.Phase == TurnPhase.Claim && command.Troops != 1)
        {
            return Reject(GameErrorCode.InvalidTroopCount, "During Claim, exactly one troop is placed per claim.");
        }

        var updatedTerritories = new Dictionary<TerritoryId, TerritoryState>(state.Territories)
        {
            [command.Territory] = territory with { Owner = command.Actor, Troops = command.Troops }
        };

        var updatedPlayer = player with { TroopsRemaining = player.TroopsRemaining - command.Troops };
        IReadOnlyList<PlayerState> updatedPlayers = state.Players
            .Select(p => p.Id == updatedPlayer.Id ? updatedPlayer : p)
            .ToArray();

        var events = new List<GameEvent> { new TerritoryClaimed(command.Actor, command.Territory, command.Troops) };
        var nextTurn = AdvanceAfterClaim(state.Turn, state.Players, updatedTerritories, events);

        var newState = state with
        {
            Territories = updatedTerritories,
            Players = updatedPlayers,
            Turn = nextTurn,
            Log = [.. state.Log, .. events]
        };

        return new CommandResult<GameState, GameEvent>.Ok(newState, events);
    }

    /// <summary>
    /// Rotation/transition logic for <see cref="ExecuteClaimTerritory"/>
    /// (design D1/D3): always rotates to the next player first (plain index,
    /// no <c>TroopsRemaining</c>/<c>IsEliminated</c> eligibility skip — every
    /// player is always eligible to claim until territories run out, and D2
    /// guarantees troops always outlast territories), then checks whether
    /// <paramref name="territories"/> still has any unowned entry. If so,
    /// the phase stays <see cref="TurnPhase.Claim"/> at the rotated player
    /// with no event. If every territory is now owned, this was the final
    /// claim: transitions to <see cref="TurnPhase.Setup"/> at the rotated
    /// player (not the claimer, and not a reset to <c>players[0]</c> — the
    /// rulebook's normal turn-taking continues straight through the phase
    /// boundary) and emits <see cref="PhaseChanged"/>.
    /// </summary>
    private static TurnState AdvanceAfterClaim(
        TurnState turn,
        IReadOnlyList<PlayerState> players,
        IReadOnlyDictionary<TerritoryId, TerritoryState> territories,
        List<GameEvent> events)
    {
        var currentIndex = players.ToList().FindIndex(p => p.Id == turn.CurrentPlayer);
        var next = players[(currentIndex + 1) % players.Count].Id;

        if (territories.Values.Any(t => t.Owner is null))
        {
            return new TurnState(next, TurnPhase.Claim);
        }

        events.Add(new PhaseChanged(TurnPhase.Claim, TurnPhase.Setup, next));
        return new TurnState(next, TurnPhase.Setup);
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

        // Reachable in production since item 2.1: a Classic game that hasn't
        // finished its Claim phase yet still has unclaimed (Owner: null)
        // territories. Kept separate from the guard above (design D6) so the
        // two rejection messages stay distinct, and so the `.Value` unwraps
        // below are provably unreachable-by-construction rather than a
        // latent InvalidOperationException.
        if (defenderTerritory.Owner is null)
        {
            return Reject(GameErrorCode.NotOwner, "The target territory has not been claimed by anyone yet.");
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
            events.Add(new TerritoryConquered(command.Actor, defenderTerritory.Owner!.Value, command.To));
            nextTurn = state.Turn with
            {
                ConqueredThisTurn = true,
                PendingOccupation = new PendingOccupation(command.From, command.To, command.DiceCount)
            };

            var defenderOwnsAnyTerritory = updatedTerritories.Values.Any(t => t.Owner == defenderTerritory.Owner);
            if (!defenderOwnsAnyTerritory)
            {
                updatedPlayers = EliminatePlayer(state.Players, defenderTerritory.Owner!.Value, command.Actor, events);

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
            else if (victoryRuleFor(state.Mode) is { } modeVictoryRule)
            {
                // Classic, via ConquestVictoryRule (item 2.1) — resolved through
                // VictoryRules.For, not hardcoded, so the test seam (design D5)
                // can prove this branch is actually reached.
                var postConquest = state with { Territories = updatedTerritories, Players = updatedPlayers, Turn = nextTurn };
                if (modeVictoryRule.CheckVictory(postConquest) is { } winner)
                {
                    newStatus = new GameStatus.Won(winner);
                    events.Add(new GameWon(winner));
                }
            }
            else
            {
                // Pre-refactor inline check, byte-identical (Capital only — the last
                // mode without an IVictoryRule; VictoryRules.For returns null. Capital's
                // real rule is roadmap item 5.3.) TwoPlayer moved to the branch above in 4.3.
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
    /// The next non-eliminated, non-neutral player after <paramref name="fromIndex"/>,
    /// wrapping around the player list. Eliminated players are skipped so
    /// the turn cycle never lands on someone with no territories left.
    /// Neutral players (<see cref="PlayerState.IsNeutral"/>, item 4.1's
    /// <see cref="GameMode.TwoPlayer"/> third army) are skipped too (item
    /// 4.2/D1): the neutral is a board object, not an agent, and must never
    /// become <see cref="TurnState.CurrentPlayer"/> nor receive reinforcement
    /// via <see cref="AdvanceToNextPlayer"/>'s unconditional
    /// <see cref="Reinforcement.Calculate"/> call.
    /// </summary>
    private static PlayerState NextActivePlayer(IReadOnlyList<PlayerState> players, int fromIndex)
    {
        for (var offset = 1; offset <= players.Count; offset++)
        {
            var candidate = players[(fromIndex + offset) % players.Count];
            if (!candidate.IsEliminated && !candidate.IsNeutral)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unreachable: at least one active player must remain once the game is won.");
    }

    /// <summary>
    /// Rotation for Setup-phase placement (design D1/D4). Only ever hands the
    /// turn to a non-neutral player — a neutral
    /// (<see cref="PlayerState.IsNeutral"/>, item 4.1's <see cref="GameMode.TwoPlayer"/>
    /// third army) never becomes <see cref="TurnState.CurrentPlayer"/>: it is
    /// a board object, not an agent, and only <c>PlaceNeutralTroopsCommand</c>
    /// spends its pool. Three cases, checked in order:
    /// (1) a non-neutral player still has their own Setup troops to place
    /// (Phase A) — rotate to them;
    /// (2) no non-neutral player has troops left, but a neutral does
    /// (<see cref="GameMode.TwoPlayer"/>'s Phase B) — keep alternating the
    /// same humans by plain index rotation, skipping only the neutral,
    /// so a human can submit the next <c>PlaceNeutralTroopsCommand</c>;
    /// (3) neither — Setup is fully complete (every mode's normal path, and
    /// TwoPlayer's Phase B-drained path) and the first player begins the
    /// normal turn cycle in Reinforce.
    /// </summary>
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
            if (!candidate.IsNeutral && candidate.TroopsRemaining > 0)
            {
                return (turn with { CurrentPlayer = candidate.Id }, players);
            }
        }

        var neutral = players.FirstOrDefault(p => p.IsNeutral);
        if (neutral is { TroopsRemaining: > 0 })
        {
            for (var offset = 1; offset <= players.Count; offset++)
            {
                var candidate = players[(currentIndex + offset) % players.Count];
                if (!candidate.IsNeutral)
                {
                    return (turn with { CurrentPlayer = candidate.Id }, players);
                }
            }
        }

        // Every non-neutral player has placed all starting troops, and (for
        // TwoPlayer) the neutral's pool is also fully drained: setup is
        // complete and the first player begins the normal turn cycle in
        // Reinforce.
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
