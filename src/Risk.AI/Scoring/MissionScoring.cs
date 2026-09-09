using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine.Views;

namespace Risk.AI.Scoring;

/// <summary>
/// Mission-aware strategic weighting, reading ONLY <see cref="PlayerView.OwnEffectiveMission"/>
/// (design D2/bot-objective-awareness). Returns <c>0.0</c> whenever the viewer holds no
/// mission (Classic and every other non-<see cref="Risk.Engine.State.GameMode.SecretMission"/>
/// mode), so generic scoring is unaffected there — this is the "weighting plug-in" tier's
/// whole point: Classic falls through for free.
/// </summary>
internal static class MissionScoring
{
    /// <summary>
    /// The minimum <see cref="OccupyTerritories.MinArmiesPerTerritory"/> at which topping
    /// up a territory's garrison is a distinct mission event — every occupied territory
    /// already carries at least 1 troop, so a threshold of 1 could never be "crossed" by
    /// a placement, and 0/1 are structural rather than tunable values (D10).
    /// </summary>
    private const int MinArmiesRequiringTopUp = 2;

    /// <summary>
    /// Strategic value of capturing <paramref name="target"/> toward
    /// <paramref name="self"/>'s effective mission. Archetype-specific:
    /// see the design's Mission archetype handling table.
    /// </summary>
    public static double GainForCapturing(PlayerView view, PlayerId self, TerritoryId target) =>
        view.OwnEffectiveMission switch
        {
            null => 0.0,
            OccupyTerritories(var count, var minArmies) => OccupyCapturingGain(view, self, count, minArmies),
            ConquerContinents(var required, var wildcardCount) =>
                ConquerContinentsCapturingGain(view, self, target, required, wildcardCount),
            EliminateArmy(var army) => EliminateArmyCapturingGain(view, self, target, army),
            _ => 0.0
        };

    /// <summary>
    /// Strategic value of placing <paramref name="troopsToAdd"/> reinforcements on
    /// <paramref name="own"/> toward <paramref name="self"/>'s effective mission. Only
    /// <see cref="OccupyTerritories"/> with <c>MinArmiesPerTerritory &gt;= 2</c> needs a
    /// distribution/top-up mode; every other archetype (and Classic) is <c>0</c>.
    /// </summary>
    public static double GainForReinforcing(PlayerView view, PlayerId self, TerritoryId own, int troopsToAdd) =>
        view.OwnEffectiveMission switch
        {
            OccupyTerritories(_, var minArmies) when minArmies >= MinArmiesRequiringTopUp =>
                TopUpGain(view, own, minArmies, troopsToAdd),
            _ => 0.0
        };

    private static double OccupyCapturingGain(PlayerView view, PlayerId self, int count, int minArmies)
    {
        var qualifying = TerritoryScoring.Owned(view, self)
            .Count(id => view.Territories[id].Troops >= minArmies);

        return qualifying < count ? BotWeights.MissionOccupyWeight : 0.0;
    }

    private static double TopUpGain(PlayerView view, TerritoryId own, int minArmies, int troopsToAdd)
    {
        var current = view.Territories[own].Troops;
        var after = current + troopsToAdd;

        return current < minArmies && after >= minArmies ? BotWeights.MissionTopUpWeight : 0.0;
    }

    private static double ConquerContinentsCapturingGain(
        PlayerView view, PlayerId self, TerritoryId target, IReadOnlyList<ContinentId> required, int wildcardCount)
    {
        var continent = Continents.All.First(c => c.Members.Contains(target));

        if (required.Contains(continent.Id))
        {
            return BotWeights.MissionContinentWeight * ProjectedOwnedFraction(view, self, continent, target);
        }

        if (wildcardCount > 0 && continent.Id.Equals(WildcardContinent(view, self, required)))
        {
            return BotWeights.MissionContinentWeight * ProjectedOwnedFraction(view, self, continent, target);
        }

        return 0.0;
    }

    /// <summary>
    /// The deterministic wildcard continent: among continents NOT in
    /// <paramref name="required"/>, the one where <paramref name="self"/> already owns
    /// the highest fraction of members, tie-broken by <see cref="ContinentId"/> ordinal.
    /// </summary>
    private static ContinentId? WildcardContinent(PlayerView view, PlayerId self, IReadOnlyList<ContinentId> required) =>
        Continents.All
            .Where(c => !required.Contains(c.Id))
            .Select(c => (c.Id, Fraction: OwnedFraction(view, self, c)))
            .OrderByDescending(x => x.Fraction)
            .ThenBy(x => x.Id.Value, StringComparer.Ordinal)
            .Select(x => (ContinentId?)x.Id)
            .FirstOrDefault();

    private static double OwnedFraction(PlayerView view, PlayerId self, Continent continent) =>
        continent.Members.Count(m => view.Territories.TryGetValue(m, out var state) && state.Owner == self)
        / (double)continent.Members.Count;

    private static double ProjectedOwnedFraction(PlayerView view, PlayerId self, Continent continent, TerritoryId target)
    {
        var ownedOtherMembers = continent.Members.Count(member =>
            !member.Equals(target) &&
            view.Territories.TryGetValue(member, out var state) &&
            state.Owner == self);

        return (ownedOtherMembers + 1) / (double)continent.Members.Count;
    }

    private static double EliminateArmyCapturingGain(PlayerView view, PlayerId self, TerritoryId target, ArmyId army)
    {
        // Defense-in-depth: OwnEffectiveMission already resolves a self-targeting
        // EliminateArmy to its OccupyTerritories fallback (MissionResolution.Effective),
        // so this branch should never legitimately see army.Value == self.Value. If it
        // ever does (malformed/test-only input), no candidate is weighted toward it.
        if (army.Value == self.Value)
        {
            return 0.0;
        }

        if (!view.Territories.TryGetValue(target, out var state) || state.Owner is not { } owner)
        {
            return 0.0;
        }

        if (owner.Value != army.Value)
        {
            return 0.0;
        }

        var targetTerritoryCount = TerritoryScoring.Owned(view, owner).Count;

        return BotWeights.MissionEliminateWeight * (1 + 1.0 / Math.Max(1, targetTerritoryCount));
    }
}
