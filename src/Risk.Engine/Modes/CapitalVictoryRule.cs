using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Engine.Modes;

/// <summary>
/// <see cref="GameMode.Capital"/>'s victory rule, implementing the mandatory
/// base rule from RISK CAPITAL (<c>reglasrisk.md</c>, p.14): a non-eliminated
/// player wins once they still own their own declared
/// <see cref="PlayerState.HeadquartersId"/> territory AND own every other
/// active player's declared headquarters territory. Losing your own HQ blocks
/// the win even if every opponent HQ is held; recapturing it re-enables
/// eligibility on the next <see cref="CheckVictory"/> call, since ownership is
/// re-evaluated fresh every time (no cached/sticky state).
/// </summary>
public sealed class CapitalVictoryRule : IVictoryRule
{
    public PlayerId? CheckVictory(GameState state)
    {
        foreach (var player in state.Players)
        {
            if (player.IsEliminated)
            {
                continue;
            }

            if (player.HeadquartersId is not { } ownHq || state.Territories[ownHq].Owner != player.Id)
            {
                continue;
            }

            // Deliberately does NOT filter eliminated opponents: an opponent's
            // HQ territory still counts as captured toward this player's win
            // once owned, even if that opponent was eliminated by someone
            // else — ownership, not elimination status, decides this check.
            var ownsAllOpponentHqs = state.Players
                .Where(o => o.Id != player.Id)
                .All(o => o.HeadquartersId is { } hq && state.Territories[hq].Owner == player.Id);

            if (ownsAllOpponentHqs)
            {
                return player.Id;
            }
        }

        return null;
    }
}
