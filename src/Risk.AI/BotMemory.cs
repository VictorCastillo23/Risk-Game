using Risk.Domain.Players;
using Risk.Engine.Events;
using Risk.Engine.State;
using Risk.Engine.Views;

namespace Risk.AI;

/// <summary>
/// The bot's own causally-derived memory across turns — everything beyond a
/// single <see cref="PlayerView"/> that a purely-reactive decision function
/// still needs (design D2/D4). Carries only values the bot can derive from
/// its own past <see cref="GameEvent"/>s: never wall-clock time, ambient
/// RNG, or engine-internal state.
/// </summary>
/// <param name="ReinforcePool">
/// Mirrors the engine's own remaining Reinforce-phase troop pool for the
/// bot's CURRENT Reinforce visit (design D2). Non-null only while inside
/// that visit; <see langword="null"/> before it starts and reset back to
/// <see langword="null"/> the instant the bot's own Reinforce phase ends.
/// Lazily seeded elsewhere (by the Reinforce decision, not by
/// <see cref="Fold"/>) from <see cref="Risk.Engine.Rules.Reinforcement.Calculate"/>;
/// <see cref="Fold"/> only ever decrements, increments, or nulls an
/// already-seeded value.
/// </param>
/// <param name="OwnSetupTroopsPlaced">
/// Cumulative count of troops the bot has placed via <see cref="TroopsPlaced"/>
/// events raised while <see cref="TurnPhase.Setup"/> was the active phase —
/// never <see cref="TerritoryClaimed"/> — used to detect the TwoPlayer Phase
/// A→B boundary (design D8).
/// </param>
/// <param name="SeenActors">
/// Every <see cref="PlayerId"/> ever observed as <see cref="TurnState.CurrentPlayer"/>
/// — public information present in every <see cref="PlayerView"/> — used to
/// infer the neutral seat by elimination (design D8).
/// </param>
public sealed record BotMemory(
    int? ReinforcePool,
    int OwnSetupTroopsPlaced,
    IReadOnlySet<PlayerId> SeenActors)
{
    /// <summary>The starting memory for a bot that has observed nothing yet.</summary>
    public static BotMemory Empty { get; } = new(null, 0, new HashSet<PlayerId>());

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
    /// Pure fold of outcome-derived memory updates (design D4) over the
    /// events produced by executing a single command. <paramref
    /// name="viewBeforeCommand"/> is the bot's own <see cref="PlayerView"/>
    /// immediately BEFORE that command executed, so its
    /// <c>Turn.Phase</c> identifies which phase the command itself
    /// belonged to — this is what distinguishes a Reinforce-phase trade
    /// from an Attack-phase mandatory overflow trade-down (design D3),
    /// where the engine's next turn-boundary reassignment of
    /// <c>TroopsRemaining</c> would silently discard any bonus banked here.
    /// </summary>
    public static BotMemory Fold(
        BotMemory memory,
        PlayerId self,
        PlayerView viewBeforeCommand,
        IReadOnlyList<GameEvent> events)
    {
        var pool = memory.ReinforcePool;
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

                case CardsTraded(var actor, _, var bonus, _) when actor == self && phase == TurnPhase.Reinforce:
                    pool += bonus;
                    break;

                case PhaseChanged(TurnPhase.Reinforce, TurnPhase.Attack, var currentPlayer) when currentPlayer == self:
                    pool = null;
                    break;
            }
        }

        return memory with { ReinforcePool = pool, OwnSetupTroopsPlaced = ownSetupTroopsPlaced };
    }
}
