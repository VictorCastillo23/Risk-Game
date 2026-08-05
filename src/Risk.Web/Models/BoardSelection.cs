using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.State;

namespace Risk.Web.Models;

/// <summary>
/// Pure, phase-aware click-selection state for the board (design's
/// "click-to-select interaction pattern"). One shared shape drives all
/// three phases so PR5 (Attack) and PR7 (Fortify) can reuse it unchanged
/// once wired: Reinforce is a single-step pick, Attack and Fortify are
/// two-step (own origin, then a phase-specific valid destination).
/// </summary>
/// <param name="Origin">The first territory picked (source for Attack/Fortify, the target itself for Reinforce).</param>
/// <param name="Destination">The second territory picked, once a phase-specific valid destination is clicked.</param>
public sealed record BoardSelection(TerritoryId? Origin, TerritoryId? Destination)
{
    /// <summary>No territory selected.</summary>
    public static readonly BoardSelection Empty = new(null, null);

    /// <summary>True once both a two-step phase's origin and destination are set.</summary>
    public bool IsComplete => Origin is not null && Destination is not null;

    /// <summary>
    /// Applies one board click for the given <paramref name="phase"/> and
    /// <paramref name="actor"/>, returning the resulting selection. Invalid
    /// clicks (e.g. an enemy territory during Reinforce, a non-adjacent
    /// enemy during Attack, a disconnected territory during Fortify) either
    /// leave the selection unchanged or re-anchor it on a newly clicked own
    /// territory — they never throw, since a click is just player input,
    /// not a rule violation.
    /// </summary>
    public BoardSelection Click(TerritoryId clicked, GameState state, PlayerId actor, TurnPhase phase)
    {
        var isOwn = state.Territories[clicked].Owner == actor;

        return phase switch
        {
            TurnPhase.Reinforce => isOwn ? new BoardSelection(clicked, null) : this,
            TurnPhase.Attack => ClickTwoStep(clicked, isOwn, isValidDestination: () => !isOwn && WorldMap.AreAdjacent(Origin!.Value, clicked)),
            TurnPhase.Fortify => ClickTwoStep(clicked, isOwn, isValidDestination: () => isOwn && HasFriendlyPath(state, Origin!.Value, clicked)),
            _ => this
        };
    }

    /// <summary>Clears the selection, e.g. after a command dispatches successfully.</summary>
    public BoardSelection Clear() => Empty;

    /// <summary>
    /// Shared two-step shape for Attack/Fortify: no origin yet → only an own
    /// click starts one; origin set → a valid destination completes the
    /// pair, an own click re-anchors the origin, anything else is ignored.
    /// </summary>
    private BoardSelection ClickTwoStep(TerritoryId clicked, bool isOwn, Func<bool> isValidDestination)
    {
        if (Origin is null)
        {
            return isOwn ? new BoardSelection(clicked, null) : this;
        }

        if (clicked != Origin.Value && isValidDestination())
        {
            return new BoardSelection(Origin, clicked);
        }

        return isOwn ? new BoardSelection(clicked, null) : this;
    }

    /// <summary>
    /// Breadth-first search restricted to territories owned by whoever owns
    /// <paramref name="from"/> — mirrors <c>GameEngine.HasFriendlyPath</c>
    /// (private to the engine) so Fortify selection previews the same
    /// connectivity rule the engine will independently enforce.
    /// </summary>
    private static bool HasFriendlyPath(GameState state, TerritoryId from, TerritoryId to)
    {
        var owner = state.Territories[from].Owner;
        var visited = new HashSet<TerritoryId> { from };
        var queue = new Queue<TerritoryId>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current == to)
            {
                return true;
            }

            foreach (var neighbor in WorldMap.NeighborsOf(current))
            {
                if (visited.Contains(neighbor) || state.Territories[neighbor].Owner != owner)
                {
                    continue;
                }

                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return false;
    }
}
