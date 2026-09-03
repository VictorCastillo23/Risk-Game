using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;

namespace Risk.Web.Models;

/// <summary>
/// Spanish display formatter for a resolved <see cref="MissionCard"/>
/// (mirrors <see cref="GameModeDisplay"/>'s exhaustive-switch pattern —
/// UI copy is Spanish, identifiers stay English). Called with whatever
/// <c>PlayerView.OwnEffectiveMission</c> already contains — this type never
/// re-derives the self-target substitution (that lives in
/// <c>Risk.Engine.Rules.MissionResolution</c>, design 3.4-D1).
/// </summary>
/// <remarks>
/// Register: VOSEO (design 3.4-D6), consistent with CardPanel's
/// "Tenés… debés…". reglasrisk.md prints these missions in tuteo, but it is
/// the source of truth for mission SEMANTICS, not for UI copy — do not
/// "restore" the tuteo wording here.
/// </remarks>
public static class MissionDisplay
{
    /// <summary>
    /// The Spanish (voseo) one-line description of <paramref name="mission"/>.
    /// <paramref name="players"/> resolves an <see cref="EliminateArmy"/>
    /// target to a display name — confirmed against
    /// <c>SecretMissionVictoryRule.cs</c>: an army's <see cref="ArmyId.Value"/>
    /// equals the seated <see cref="PlayerId.Value"/> it was dealt to. An
    /// unseated/unknown id (impossible after roadmap 3.2's seated-pool
    /// filter) falls back to "Ejército {n}" rather than throwing — a
    /// formatter must never throw while rendering.
    /// </summary>
    public static string Label(MissionCard mission, IReadOnlyDictionary<PlayerId, PlayerConfig> players) => mission switch
    {
        OccupyTerritories occupy => OccupyLabel(occupy),
        EliminateArmy eliminate => EliminateLabel(eliminate, players),
        ConquerContinents conquer => ConquerLabel(conquer),
        _ => throw new InvalidOperationException("Unreachable: unknown MissionCard archetype.")
    };

    private static string OccupyLabel(OccupyTerritories occupy) => occupy.MinArmiesPerTerritory >= 2
        ? $"Ocupá {occupy.Count} territorios con al menos {occupy.MinArmiesPerTerritory} tropas en cada uno."
        : $"Ocupá {occupy.Count} territorios.";

    private static string EliminateLabel(EliminateArmy eliminate, IReadOnlyDictionary<PlayerId, PlayerConfig> players)
    {
        var targetId = new PlayerId(eliminate.Army.Value);
        var name = players.TryGetValue(targetId, out var config)
            ? config.Name
            : $"Ejército {eliminate.Army.Value + 1}";

        return $"Destruí todas las tropas de {name}.";
    }

    private static string ConquerLabel(ConquerContinents conquer)
    {
        var required = JoinContinents(conquer.Required);

        return conquer.WildcardCount switch
        {
            0 => $"Conquistá {required}.",
            1 => $"Conquistá {required}, más 1 continente más de tu elección.",
            _ => $"Conquistá {required}, más {conquer.WildcardCount} continentes más de tu elección."
        };
    }

    private static string JoinContinents(IReadOnlyList<ContinentId> continents)
    {
        var labels = continents.Select(ContinentDisplay.Label).ToArray();

        return labels.Length switch
        {
            0 => string.Empty,
            1 => labels[0],
            _ => $"{string.Join(", ", labels[..^1])} y {labels[^1]}"
        };
    }
}
