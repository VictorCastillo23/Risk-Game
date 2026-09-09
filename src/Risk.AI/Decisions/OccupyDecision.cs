using Risk.AI.Scoring;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Views;

namespace Risk.AI.Decisions;

/// <summary>
/// Occupy-phase troop sizing (design's Decision Algorithms / Occupy, D7).
/// <c>min</c>/<c>max</c> bounds always satisfy <c>min &lt;= max</c> by
/// construction (D7's proof: a conquering battle round always has
/// <c>attackerLosses == 0</c>), so the clamp below can never collapse to an
/// invalid range.
/// </summary>
internal static class OccupyDecision
{
    public static GameCommand Decide(PlayerView view, PlayerId self)
    {
        var pending = view.Turn.PendingOccupation!;
        var min = pending.MinimumTroops;
        var max = view.Territories[pending.From].Troops - 1;

        var fromThreat = TerritoryScoring.Facts(view, self, pending.From).HostileNeighborTroops;
        var toThreat = TerritoryScoring.Facts(view, self, pending.Conquered).HostileNeighborTroops;
        var share = (double)toThreat / Math.Max(1, fromThreat + toThreat);

        var raw = min + share * (max - min);
        var troops = Math.Clamp((int)Math.Round(raw, MidpointRounding.AwayFromZero), min, max);

        return new OccupyCommand(self, troops);
    }
}
