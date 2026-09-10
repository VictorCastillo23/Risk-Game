using Risk.AI.Scoring;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.State;
using Risk.Engine.Views;

namespace Risk.AI.Decisions;

/// <summary>
/// Setup-phase placement (design's Decision Algorithms / Setup, design D8).
/// Always places exactly 1 troop — legal in every mode's Setup budget and
/// never exceeds <c>TroopsRemaining</c>, since the engine only grants a
/// Phase-A Setup turn while it is positive. Switches to
/// <see cref="PlaceNeutralTroopsCommand"/> once <see cref="BotMemory"/>
/// confirms the TwoPlayer Phase A→B boundary (design D8): <c>partyCount == 3</c>,
/// no effective mission (rules out 3-player SecretMission), exactly 2 seen
/// actors (rules out 3-player Classic/Capital, where 3 parties rotate), and
/// the bot's own cumulative Setup placements reaching 26 — TwoPlayer's own
/// remaining Setup pool after the initial 14-territory/1-troop-each deal
/// (40 starting troops − 14 dealt = 26; verified against
/// <c>TwoPlayerSetupStrategy.cs</c>/<c>GameSetup.cs</c>).
/// </summary>
internal static class SetupDecision
{
    private const int RequiredPartyCountForTwoPlayer = 3;
    private const int RequiredSeenActorsForTwoPlayer = 2;
    private const int TwoPlayerOwnSetupBudget = 26;

    public static GameCommand Decide(PlayerView view, PlayerId self, BotMemory memory)
    {
        if (TryNeutralPlacement(view, self, memory, out var neutralCommand))
        {
            return neutralCommand;
        }

        var target = MostUrgentOwned(view, self);
        return new PlaceTroopsCommand(self, target, 1);
    }

    private static bool TryNeutralPlacement(PlayerView view, PlayerId self, BotMemory memory, out GameCommand command)
    {
        command = null!;

        if (!IsTwoPlayerPhaseB(view, memory))
        {
            return false;
        }

        if (memory.FindTwoPlayerNeutral(view, self) is not { } neutral)
        {
            return false;
        }

        var parties = new HashSet<PlayerId>(view.OtherPlayersCardCounts.Keys) { self };
        var opponents = parties.Where(p => !p.Equals(self) && !p.Equals(neutral)).ToList();
        if (opponents.Count != 1)
        {
            return false;
        }

        if (BestNeutralTarget(view, self, opponents[0], neutral) is not { } territory)
        {
            return false;
        }

        command = new PlaceNeutralTroopsCommand(self, territory, 1);
        return true;
    }

    private static bool IsTwoPlayerPhaseB(PlayerView view, BotMemory memory) =>
        view.Turn.Phase == TurnPhase.Setup
        && view.OtherPlayersCardCounts.Count + 1 == RequiredPartyCountForTwoPlayer
        && view.OwnEffectiveMission is null
        && memory.SeenActors.Count == RequiredSeenActorsForTwoPlayer
        && memory.OwnSetupTroopsPlaced >= TwoPlayerOwnSetupBudget;

    private static TerritoryId MostUrgentOwned(PlayerView view, PlayerId self) =>
        TerritoryScoring.Owned(view, self)
            .OrderByDescending(id => TerritoryScoring.DefenseUrgency(TerritoryScoring.Facts(view, self, id)))
            .ThenBy(TerritoryScoring.IndexOf)
            .First();

    /// <summary>
    /// The neutral-owned territory that most disadvantages
    /// <paramref name="opponent"/>: highest count of neighbors owned by
    /// <paramref name="opponent"/> minus neighbors owned by
    /// <paramref name="self"/> — reinforcing a border the opponent already
    /// presses on, rather than one already safely fronting the bot's own
    /// territory.
    /// </summary>
    private static TerritoryId? BestNeutralTarget(PlayerView view, PlayerId self, PlayerId opponent, PlayerId neutral)
    {
        var neutralTerritories = view.Territories
            .Where(kv => kv.Value.Owner == neutral)
            .Select(kv => kv.Key)
            .ToList();

        if (neutralTerritories.Count == 0)
        {
            return null;
        }

        return neutralTerritories
            .OrderByDescending(id => PressureOnOpponent(view, self, opponent, id))
            .ThenBy(TerritoryScoring.IndexOf)
            .First();
    }

    private static int PressureOnOpponent(PlayerView view, PlayerId self, PlayerId opponent, TerritoryId id)
    {
        var opponentNeighbors = 0;
        var ownNeighbors = 0;

        foreach (var neighborId in WorldMap.NeighborsOf(id))
        {
            if (!view.Territories.TryGetValue(neighborId, out var neighbor))
            {
                continue;
            }

            if (neighbor.Owner == opponent)
            {
                opponentNeighbors++;
            }
            else if (neighbor.Owner == self)
            {
                ownNeighbors++;
            }
        }

        return opponentNeighbors - ownNeighbors;
    }
}
