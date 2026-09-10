using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;

namespace Risk.AI;

/// <summary>
/// The single observe → decide → execute step shared by <see cref="BotTurnRunner"/>'s
/// production loop and <c>Risk.AI.Tests</c>' <c>GameHarness</c> fixture — both
/// need the IDENTICAL sequence (thread every tracked bot's memory through
/// <see cref="BotMemory.WithSeenActor"/> first, design D8; then
/// <see cref="IGameEngine.Observe"/>; then <see cref="IBotPlayer.DecideNextCommand"/>;
/// then <see cref="IGameEngine.Execute"/>; then <see cref="BotMemory.Fold"/> on
/// success), so this extraction is what keeps them from silently drifting
/// apart if that sequence ever gains a step. Deliberately returns the raw
/// <see cref="CommandResult{TState,TEvent}"/> rather than making a
/// success/failure decision itself: <see cref="BotTurnRunner"/> turns a
/// <c>Rejected</c> into a terminal <see cref="BotRunResult.Rejected"/>, while
/// <c>GameHarness</c> throws — both are the CALLER's decision, not this
/// helper's.
/// </summary>
internal static class BotTurnStep
{
    /// <param name="memories">
    /// Every tracked player's <see cref="BotMemory"/>, keyed by
    /// <see cref="PlayerId"/>; MUST already contain an entry for
    /// <paramref name="actor"/>. Mutated in place: every entry observes
    /// <paramref name="actor"/> via <see cref="BotMemory.WithSeenActor"/>
    /// before <paramref name="bot"/> decides, and <paramref name="actor"/>'s
    /// own entry is updated again afterward (via <see cref="BotMemory.Fold"/>
    /// on success, or left as the bot's own pre-Fold decided memory on
    /// rejection).
    /// </param>
    /// <returns>
    /// The raw <see cref="CommandResult{TState,TEvent}"/>, the exact
    /// <see cref="GameCommand"/> the bot issued, and the resulting state —
    /// unchanged from <paramref name="state"/> on <c>Rejected</c> (the
    /// engine's own guarantee, passed through untouched), or the new state
    /// on <c>Ok</c>.
    /// </returns>
    internal static (CommandResult<GameState, GameEvent> Result, GameCommand Command, GameState State) Advance(
        IGameEngine engine,
        GameState state,
        PlayerId actor,
        IBotPlayer bot,
        Dictionary<PlayerId, BotMemory> memories)
    {
        foreach (var id in memories.Keys.ToArray())
        {
            memories[id] = memories[id].WithSeenActor(actor);
        }

        var view = engine.Observe(state, actor);
        var (command, decidedMemory) = bot.DecideNextCommand(view, memories[actor]);
        var result = engine.Execute(state, command);

        if (result is CommandResult<GameState, GameEvent>.Rejected)
        {
            memories[actor] = decidedMemory;
            return (result, command, state);
        }

        var ok = (CommandResult<GameState, GameEvent>.Ok)result;
        memories[actor] = BotMemory.Fold(decidedMemory, actor, view, ok.Events);
        return (result, command, ok.State);
    }
}
