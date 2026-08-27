using Risk.Domain.Map;

namespace Risk.Web.Models;

/// <summary>
/// Pure, client-side-only staged troop placements for Reinforce — never
/// dispatched to <c>Risk.Engine</c> until confirmed. <c>PlaceTroopsCommand</c>
/// only ever adds troops (no undo exists once dispatched, by design — matching
/// real Risk), so a "take it back" gesture here can only ever un-stage what
/// THIS staging session added locally; it can never touch a territory's
/// committed troop count from before the current Reinforce turn.
/// </summary>
/// <param name="Pending">Territories with a nonzero staged troop count.</param>
public sealed record TroopStaging(IReadOnlyDictionary<TerritoryId, int> Pending)
{
    public static readonly TroopStaging Empty = new(new Dictionary<TerritoryId, int>());

    /// <summary>Sum of every territory's staged troop count.</summary>
    public int TotalStaged => Pending.Values.Sum();

    /// <summary>The staged troop count for <paramref name="territory"/>, or 0 if none.</summary>
    public int For(TerritoryId territory) => Pending.GetValueOrDefault(territory);

    /// <summary>
    /// Stages one more troop for <paramref name="territory"/>, unless the
    /// player's live <paramref name="remainingPool"/> (<c>PlayerState.TroopsRemaining</c>)
    /// is already fully staged — a no-op past that ceiling, since staging
    /// more than the engine will actually let the player place is pointless.
    /// </summary>
    public TroopStaging Add(TerritoryId territory, int remainingPool)
    {
        if (TotalStaged >= remainingPool)
        {
            return this;
        }

        var updated = new Dictionary<TerritoryId, int>(Pending)
        {
            [territory] = For(territory) + 1
        };

        return new TroopStaging(updated);
    }

    /// <summary>
    /// Un-stages one troop from <paramref name="territory"/>, removing the
    /// entry once it reaches zero. A no-op if nothing is staged there — this
    /// can never go negative, so it can never touch troops the territory
    /// already had before this Reinforce turn.
    /// </summary>
    public TroopStaging Remove(TerritoryId territory)
    {
        if (!Pending.TryGetValue(territory, out var count))
        {
            return this;
        }

        var updated = new Dictionary<TerritoryId, int>(Pending);

        if (count <= 1)
        {
            updated.Remove(territory);
        }
        else
        {
            updated[territory] = count - 1;
        }

        return new TroopStaging(updated);
    }

    /// <summary>Clears every staged territory, e.g. after a successful confirm.</summary>
    public TroopStaging Clear() => Empty;

    public bool Equals(TroopStaging? other) =>
        other is not null && Pending.Count == other.Pending.Count && Pending.All(kv => other.Pending.TryGetValue(kv.Key, out var v) && v == kv.Value);

    public override int GetHashCode() => Pending.Aggregate(0, (hash, kv) => hash ^ kv.Key.GetHashCode() ^ kv.Value.GetHashCode());
}
