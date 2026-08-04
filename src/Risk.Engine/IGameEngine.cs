using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Engine.Views;

namespace Risk.Engine;

/// <summary>
/// The public contract every client (tests today; Risk.AI/Risk.Web later)
/// drives the game through: mutate state via <see cref="Execute"/>, or read
/// a redacted view via <see cref="Observe"/>. All rule validation happens
/// inside the implementation; callers never pre-validate.
/// </summary>
public interface IGameEngine
{
    CommandResult<GameState, GameEvent> Execute(GameState state, GameCommand command);

    PlayerView Observe(GameState state, PlayerId viewer);
}
