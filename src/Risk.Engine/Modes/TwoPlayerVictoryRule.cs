using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Engine.Modes;

/// <summary>
/// <see cref="GameMode.TwoPlayer"/>'s victory rule: reports the sole
/// surviving human player once the other human is eliminated. Unlike
/// <see cref="ConquestVictoryRule"/>'s "owns all 42 territories" check, this
/// rule never counts territory — the engine-created neutral player
/// (<see cref="PlayerState.IsNeutral"/>) legitimately and permanently holds
/// territory it never gives up, so an "owns everything" check would be
/// unreachable in this mode. Excluding the neutral from the survivor count
/// is what makes elimination-based, not territory-based, detection correct
/// here.
/// </summary>
public sealed class TwoPlayerVictoryRule : IVictoryRule
{
    public PlayerId? CheckVictory(GameState state)
    {
        var survivors = state.Players.Where(p => !p.IsNeutral && !p.IsEliminated).ToList();
        return survivors.Count == 1 ? survivors[0].Id : null;
    }
}
