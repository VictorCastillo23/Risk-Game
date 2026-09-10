using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Views;

namespace Risk.AI;

/// <summary>
/// The bot's public decision contract, locked by the proposal and the spec's
/// "IBotPlayer / BotMemory pure-decision contract" requirement: a pure
/// function from a redacted <see cref="PlayerView"/> plus the bot's own
/// <see cref="BotMemory"/> to the next <see cref="GameCommand"/> to issue and
/// the memory to carry forward. Given an equal <paramref name="view"/>/
/// <paramref name="memory"/> pair, repeated calls MUST return equal results —
/// no reliance on wall-clock time, ambient RNG, or static/mutable state.
/// </summary>
public interface IBotPlayer
{
    /// <summary>The seat this bot plays as.</summary>
    PlayerId Id { get; }

    (GameCommand Command, BotMemory Memory) DecideNextCommand(PlayerView view, BotMemory memory);
}
