using Risk.Domain.Missions;
using Risk.Domain.Players;

namespace Risk.Engine.Rules;

/// <summary>
/// Resolves a DEALT MissionCard to the one that actually governs the holder
/// (reglasrisk.md:83-85). A dealt EliminateArmy naming the holder's own seat
/// resolves to its printed fallback, OccupyTerritories(24, 1);
/// PlayerState.Mission is never rewritten (roadmap 3.2 deferral).
/// Deliberately `internal`: the two legitimate consumers are
/// SecretMissionVictoryRule (what completion means) and GameEngine.Observe
/// (what the holder is told), both inside Risk.Engine. Risk.Web/Risk.AI must
/// consume PlayerView.OwnEffectiveMission, never re-derive this.
/// Visible to Risk.Tests via Risk.Engine.csproj's InternalsVisibleTo.
/// </summary>
internal static class MissionResolution
{
    internal static MissionCard Effective(PlayerId player, MissionCard dealt) =>
        dealt is EliminateArmy(var army) && army.Value == player.Value
            ? new OccupyTerritories(24, MinArmiesPerTerritory: 1)
            : dealt;
}
