using Risk.Domain.Players;
using Risk.Engine.Events;
using Risk.Engine.State;
using Risk.Engine.Views;

namespace Risk.AI;

/// <summary>
/// The bot's own causally-derived memory across turns — everything beyond a
/// single <see cref="PlayerView"/> that a purely-reactive decision function
/// still needs. Carries only values the bot can derive from
/// its own past <see cref="GameEvent"/>s: never wall-clock time, ambient
/// RNG, or engine-internal state.
/// </summary>
/// <param name="ReinforcePool">
/// Mirrors the engine's own remaining Reinforce-phase troop pool for the
/// bot's CURRENT or UPCOMING Reinforce visit. Reset to
/// <see langword="null"/> the instant the bot's own Reinforce phase ends
/// (<c>PhaseChanged</c> Reinforce → Attack, self), and stays
/// <see langword="null"/> until either the next Reinforce visit's base
/// amount is seeded or a <see cref="CardsTraded"/> bonus for the bot's own
/// trade is banked early — whichever happens first. That means this field
/// can already be non-null WHILE the bot is still in Attack or Fortify
/// phase, carrying nothing but a banked bonus with no base amount yet: a
/// mandatory overflow trade-down forced mid-Attack by an elimination (see
/// <c>TurnState.MandatoryTradeDown</c>) banks its bonus here immediately,
/// and <c>GameEngine</c> genuinely preserves it — additively — all the way
/// to the bot's next Reinforce visit (see
/// <c>AdvanceToNextPlayer</c>/<c>AdvanceAfterHeadquartersSelection</c>), so
/// this field must carry it for that entire span too.
/// The BASE amount (<see cref="Risk.Engine.Rules.Reinforcement.Calculate"/>)
/// is seeded lazily elsewhere (by <c>Decisions.ReinforceDecision</c>, not by
/// <see cref="Fold"/>) — see the seeding contract note below.
/// <para>
/// <b>Seeding contract (binding on whatever code seeds this field, i.e.
/// <c>Decisions.ReinforceDecision</c>):</b> <c>GameEngine</c>'s
/// <c>mandatoryTradeAtTurnStart</c> gate can force a <c>TradeCardsCommand</c>
/// to be the bot's very FIRST command of a Reinforce visit (whenever the
/// bot's hand is already 5+ cards at turn start), which can run BEFORE any
/// code gets a chance to seed this field from
/// <see cref="Risk.Engine.Rules.Reinforcement.Calculate"/> — and, as noted
/// above, a bonus can also already be sitting in this field from an
/// Attack-phase trade banked during a PRIOR turn, before this visit even
/// begins. To make sure a bonus banked either way is never lost,
/// <see cref="Fold"/> ALWAYS banks a qualifying <see cref="CardsTraded"/>
/// bonus additively — treating a <see langword="null"/> pool as 0 rather
/// than using a lifted <c>+=</c>/<c>??</c>, which would otherwise silently
/// discard the bonus (a lifted nullable <c>+=</c> evaluates to
/// <see langword="null"/> whenever either operand is <see langword="null"/>).
/// Consequently, whatever seeds this field from
/// <see cref="Risk.Engine.Rules.Reinforcement.Calculate"/>
/// MUST NOT unconditionally coalesce
/// (<c>memory.ReinforcePool ?? Reinforcement.Calculate(...)</c>) and treat a
/// non-null read as "already fully seeded, nothing to add" — a non-null
/// value at the first decision point of a visit may be nothing but an
/// early-banked trade bonus (from this visit's own mandatory trade-at-start,
/// or carried over from a prior turn's Attack-phase trade-down) with the
/// base allotment still missing. The base amount must be ADDED exactly once
/// per visit, not skipped whenever the pool happens to already be non-null.
/// This is exactly what <see cref="ReinforcePoolSeeded"/> exists to
/// disambiguate: a non-null pool alone cannot tell "bonus banked, base still
/// missing" apart from "base already added, partially spent" — only
/// <see cref="ReinforcePoolSeeded"/> can.
/// </para>
/// </param>
/// <param name="ReinforcePoolSeeded">
/// Whether the base allotment (<see cref="Risk.Engine.Rules.Reinforcement.Calculate"/>)
/// has already been added to <see cref="ReinforcePool"/> for the bot's
/// CURRENT Reinforce visit. Needed because <see cref="ReinforcePool"/> alone
/// is ambiguous: it can be non-null either because the base was already
/// seeded (and possibly partially spent), or because only an early-banked
/// trade bonus landed via <see cref="Fold"/> before the base had a chance to
/// seed (see the seeding contract above). Reset to <see langword="false"/>
/// in lockstep with <see cref="ReinforcePool"/> resetting to
/// <see langword="null"/>, at the same <see cref="PhaseChanged"/>
/// (Reinforce → Attack, self) boundary.
/// </param>
/// <param name="OwnSetupTroopsPlaced">
/// Cumulative count of troops the bot has placed via <see cref="TroopsPlaced"/>
/// events raised while <see cref="TurnPhase.Setup"/> was the active phase —
/// never <see cref="TerritoryClaimed"/> — used to detect the TwoPlayer Phase
/// A→B boundary.
/// </param>
/// <param name="SeenActors">
/// Every <see cref="PlayerId"/> ever observed as <see cref="TurnState.CurrentPlayer"/>
/// — public information present in every <see cref="PlayerView"/> — used to
/// infer the neutral seat by elimination.
/// </param>
public sealed record BotMemory(
    int? ReinforcePool,
    bool ReinforcePoolSeeded,
    int OwnSetupTroopsPlaced,
    IReadOnlySet<PlayerId> SeenActors)
{
    /// <summary>The starting memory for a bot that has observed nothing yet.</summary>
    public static BotMemory Empty { get; } = new(null, false, 0, new HashSet<PlayerId>());

    /// <summary>
    /// Returns a memory with <paramref name="actor"/> recorded as seen.
    /// Returns this same instance, unchanged, when <paramref name="actor"/>
    /// was already seen.
    /// </summary>
    public BotMemory WithSeenActor(PlayerId actor)
    {
        if (SeenActors.Contains(actor))
        {
            return this;
        }

        return this with { SeenActors = new HashSet<PlayerId>(SeenActors) { actor } };
    }

    /// <summary>
    /// The TwoPlayer neutral army's <see cref="PlayerId"/>, inferred by
    /// elimination: the one party among <paramref name="self"/>
    /// and every key of <see cref="PlayerView.OtherPlayersCardCounts"/> that
    /// has never been recorded in <see cref="SeenActors"/> — the neutral army
    /// never becomes <see cref="TurnState.CurrentPlayer"/>, so it is the only
    /// party that can never be "seen". Returns <see langword="null"/> unless
    /// exactly one such party exists (e.g. every party has already been seen,
    /// as in a 3-player game with no neutral, or more than one party remains
    /// unseen early in the game before rotation has covered every seat).
    /// </summary>
    public PlayerId? FindTwoPlayerNeutral(PlayerView view, PlayerId self)
    {
        var parties = new HashSet<PlayerId>(view.OtherPlayersCardCounts.Keys) { self };
        var unseen = parties.Except(SeenActors).ToList();

        return unseen.Count == 1 ? unseen[0] : null;
    }

    /// <summary>
    /// Pure fold of outcome-derived memory updates over the
    /// events produced by executing a single command. <paramref
    /// name="viewBeforeCommand"/> is the bot's own <see cref="PlayerView"/>
    /// immediately BEFORE that command executed, so its <c>Turn.Phase</c>
    /// identifies which phase the command itself belonged to — used to tell
    /// a Reinforce-phase troop placement (which spends the pool) apart from
    /// a Setup-phase one (which does not). A <see cref="CardsTraded"/> bonus
    /// for the bot's own trade is banked regardless of phase: <c>GameEngine</c>
    /// genuinely preserves an Attack-phase mandatory overflow trade-down's
    /// bonus additively across the next turn boundary (see
    /// <c>AdvanceToNextPlayer</c>/<c>AdvanceAfterHeadquartersSelection</c>),
    /// so this fold must track it too, not discard it.
    /// </summary>
    public static BotMemory Fold(
        BotMemory memory,
        PlayerId self,
        PlayerView viewBeforeCommand,
        IReadOnlyList<GameEvent> events)
    {
        var pool = memory.ReinforcePool;
        var reinforcePoolSeeded = memory.ReinforcePoolSeeded;
        var ownSetupTroopsPlaced = memory.OwnSetupTroopsPlaced;
        var phase = viewBeforeCommand.Turn.Phase;

        foreach (var gameEvent in events)
        {
            switch (gameEvent)
            {
                case TroopsPlaced(var player, _, var troops) when player == self && phase == TurnPhase.Reinforce:
                    pool -= troops;
                    break;

                case TroopsPlaced(var player, _, var troops) when player == self && phase == TurnPhase.Setup:
                    ownSetupTroopsPlaced += troops;
                    break;

                case CardsTraded(var actor, _, var bonus, _) when actor == self:
                    // Banked regardless of phase: GameEngine's
                    // AdvanceToNextPlayer/AdvanceAfterHeadquartersSelection
                    // now ADD the next reinforcement to whatever
                    // TroopsRemaining already holds instead of overwriting
                    // it, so a bonus banked mid-Attack (a mandatory overflow
                    // trade-down forced by an elimination — see
                    // TurnState.MandatoryTradeDown) genuinely survives to the
                    // bot's next Reinforce visit and must be tracked here too.
                    //
                    // Additive, never a lifted `+=`/`??`: the pool may still
                    // be null here (not yet seeded from Reinforcement.Calculate)
                    // when a mandatory trade-at-turn-start fires before any
                    // seeding step runs. A lifted `+=` on a null pool would
                    // silently discard this bonus — see the seeding contract
                    // note on the ReinforcePool parameter above.
                    pool = (pool ?? 0) + bonus;
                    break;

                case PhaseChanged(TurnPhase.Reinforce, TurnPhase.Attack, var currentPlayer) when currentPlayer == self:
                    pool = null;
                    reinforcePoolSeeded = false;
                    break;
            }
        }

        return memory with
        {
            ReinforcePool = pool,
            ReinforcePoolSeeded = reinforcePoolSeeded,
            OwnSetupTroopsPlaced = ownSetupTroopsPlaced
        };
    }
}
