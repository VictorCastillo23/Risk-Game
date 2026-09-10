using Risk.AI.Decisions;
using Risk.AI.Scoring;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.State;
using Risk.Engine.Views;

namespace Risk.AI;

/// <summary>
/// The concrete deterministic heuristic bot (design's Decision Algorithms /
/// BotPlayer dispatch). <see cref="DecideNextCommand"/>'s precedence
/// byte-mirrors <see cref="Risk.Engine.GameEngine.Execute"/>'s own
/// validation pipeline, so the bot never issues a command the engine would
/// reject for a pure ordering reason: a pending occupation always wins over
/// everything else, then a mandatory card trade, then the phase switch.
/// </summary>
/// <remarks>
/// Every <c>Decisions.*</c> call below is reached only when its own
/// phase/pending-state precondition structurally holds, by construction of
/// this dispatch order — never as a runtime probe:
/// <list type="bullet">
/// <item><see cref="OccupyDecision"/> is called only when
/// <see cref="TurnState.PendingOccupation"/> is not null, checked first,
/// exactly like <c>GameEngine.Execute</c>'s own pending-occupation gate.</item>
/// <item><see cref="ClaimDecision"/>/<see cref="SetupDecision"/> are called
/// only during <see cref="TurnPhase.Claim"/>/<see cref="TurnPhase.Setup"/>,
/// which the engine only keeps active while at least one unclaimed territory
/// remains, or while the actor still has a Setup budget and therefore
/// already owns territory from the completed Claim phase — so their
/// internal <c>.First()</c> over candidate sequences can never see an empty
/// sequence here.</item>
/// <item><see cref="HeadquartersDecision"/> is called only during
/// <see cref="TurnPhase.SelectHeadquarters"/> (Capital mode only), which the
/// engine only enters after Setup has completed and every player already
/// owns territory.</item>
/// </list>
/// None of these preconditions are re-validated defensively here — they are
/// guaranteed by the engine's own phase machinery, the same way
/// <c>Decisions/*</c>'s own doc comments already describe. If a future
/// engine change ever made one of these phases reachable without its
/// current precondition, the corresponding <c>Decisions.*</c> method would
/// throw rather than silently misbehave, which is the intended fail-fast
/// behavior for a programmer error (repo convention).
/// </remarks>
public sealed class BotPlayer : IBotPlayer
{
    // Mirrors GameEngine.Execute's private MandatoryTradeThreshold; kept
    // local since that constant is private to GameEngine.
    private const int MandatoryTradeHandThreshold = 5;

    public BotPlayer(PlayerId id) => Id = id;

    public PlayerId Id { get; }

    public (GameCommand Command, BotMemory Memory) DecideNextCommand(PlayerView view, BotMemory memory)
    {
        if (view.Turn.PendingOccupation is not null)
        {
            return (OccupyDecision.Decide(view, Id), memory);
        }

        if (IsMandatoryTradeDue(view))
        {
            var trade = CardTradeSelection.BestTrade(view, Id)
                ?? throw new InvalidOperationException(
                    "Unreachable: a hand of 5+ cards drawn from only 3 symbols plus at most 2 wildcards always yields a CardSet.IsValid combination.");

            return (trade, memory);
        }

        return view.Turn.Phase switch
        {
            TurnPhase.Claim => (ClaimDecision.Decide(view, Id), memory),
            TurnPhase.Setup => (SetupDecision.Decide(view, Id, memory), memory),
            TurnPhase.SelectHeadquarters => (HeadquartersDecision.Decide(view, Id), memory),
            TurnPhase.Reinforce => ReinforceDecision.Decide(view, Id, memory),
            TurnPhase.Attack => (AttackDecision.Decide(view, Id, memory), memory),
            TurnPhase.Fortify => (FortifyDecision.Decide(view, Id), memory),
            _ => throw new InvalidOperationException("Unreachable: unknown TurnPhase.")
        };
    }

    /// <summary>
    /// Collapses <c>GameEngine.Execute</c>'s two mandatory-trade clauses into
    /// the single "is a trade due right now" test the design's dispatch
    /// algorithm calls for: a mandatory overflow trade-down armed mid-Attack
    /// (<see cref="TurnState.MandatoryTradeDown"/>), OR the turn-start
    /// Reinforce-phase rule (a 5+ card hand carried into Reinforce). Both
    /// require a 5+ card hand to actually be blocking — <c>Turn.Phase ==
    /// Reinforce</c> alone with a smaller hand is not mandatory, it is just
    /// <see cref="ReinforceDecision"/>'s own eager-but-voluntary trade.
    /// </summary>
    private static bool IsMandatoryTradeDue(PlayerView view) =>
        (view.Turn.MandatoryTradeDown || view.Turn.Phase == TurnPhase.Reinforce)
        && view.OwnHand.Count >= MandatoryTradeHandThreshold;
}
