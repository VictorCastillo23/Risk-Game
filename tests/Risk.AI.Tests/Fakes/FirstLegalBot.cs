using Risk.AI.Scoring;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Rules;
using Risk.Engine.State;
using Risk.Engine.Views;

namespace Risk.AI.Tests.Fakes;

/// <summary>
/// A trivial, always-legal, test-only <see cref="IBotPlayer"/> that always
/// picks the lowest <see cref="TerritoryScoring.IndexOf"/> legal option —
/// never the production <see cref="BotPlayer"/> — so <see cref="GameHarness"/>
/// (and any other fixture that needs to drive the real engine forward) is
/// never the same code as the system under test. Deliberately never
/// attacks, so hands never accumulate cards and games driven purely by this
/// bot never reach <see cref="GameStatus.Won"/> — useful both for
/// fast-forwarding to a target phase and for exercising a runner's
/// <c>Exhausted</c> path deterministically.
/// </summary>
internal sealed class FirstLegalBot : IBotPlayer
{
    // Mirrors Decisions.SetupDecision's independently-verified TwoPlayer
    // Phase A/B boundary (design D8): 40 starting troops - 14 dealt at setup
    // (42 territories / 3 parties) = 26 remaining own-placement troops.
    // Duplicated here (not reused directly from SetupDecision) so this
    // fixture's own correctness never depends on the exact production
    // decision code a Phase 8 test might be trying to exercise.
    private const int TwoPlayerPartyCount = 3;
    private const int TwoPlayerSeenActorsAtBoundary = 2;
    private const int TwoPlayerOwnSetupBudget = 26;

    public FirstLegalBot(PlayerId id) => Id = id;

    public PlayerId Id { get; }

    public (GameCommand Command, BotMemory Memory) DecideNextCommand(PlayerView view, BotMemory memory)
    {
        if (view.Turn.PendingOccupation is { } pending)
        {
            return (new OccupyCommand(Id, pending.MinimumTroops), memory);
        }

        return view.Turn.Phase switch
        {
            TurnPhase.Claim => (DecideClaim(view), memory),
            TurnPhase.Setup => DecideSetup(view, memory),
            TurnPhase.SelectHeadquarters => (DecideHeadquarters(view), memory),
            TurnPhase.Reinforce => DecideReinforce(view, memory),
            TurnPhase.Attack => (new EndPhaseCommand(Id), memory),
            TurnPhase.Fortify => (new EndPhaseCommand(Id), memory),
            _ => throw new InvalidOperationException("Unreachable: unknown TurnPhase.")
        };
    }

    private GameCommand DecideClaim(PlayerView view)
    {
        var territory = view.Territories
            .Where(kv => kv.Value.Owner is null)
            .Select(kv => kv.Key)
            .OrderBy(TerritoryScoring.IndexOf)
            .First();

        return new ClaimTerritoryCommand(Id, territory, 1);
    }

    private (GameCommand, BotMemory) DecideSetup(PlayerView view, BotMemory memory)
    {
        if (TryNeutralPlacement(view, memory, out var neutralCommand))
        {
            return (neutralCommand, memory);
        }

        var target = view.Territories
            .Where(kv => kv.Value.Owner == Id)
            .Select(kv => kv.Key)
            .OrderBy(TerritoryScoring.IndexOf)
            .First();

        return (new PlaceTroopsCommand(Id, target, 1), memory);
    }

    private bool TryNeutralPlacement(PlayerView view, BotMemory memory, out GameCommand command)
    {
        command = null!;

        if (!IsTwoPlayerPhaseB(view, memory))
        {
            return false;
        }

        if (memory.FindTwoPlayerNeutral(view, Id) is not { } neutral)
        {
            return false;
        }

        var neutralTerritory = view.Territories
            .Where(kv => kv.Value.Owner == neutral)
            .Select(kv => kv.Key)
            .OrderBy(TerritoryScoring.IndexOf)
            .Cast<TerritoryId?>()
            .FirstOrDefault();

        if (neutralTerritory is not { } territory)
        {
            return false;
        }

        command = new PlaceNeutralTroopsCommand(Id, territory, 1);
        return true;
    }

    private static bool IsTwoPlayerPhaseB(PlayerView view, BotMemory memory) =>
        view.OtherPlayersCardCounts.Count + 1 == TwoPlayerPartyCount
        && view.OwnEffectiveMission is null
        && memory.SeenActors.Count == TwoPlayerSeenActorsAtBoundary
        && memory.OwnSetupTroopsPlaced >= TwoPlayerOwnSetupBudget;

    private GameCommand DecideHeadquarters(PlayerView view)
    {
        var territory = view.Territories
            .Where(kv => kv.Value.Owner == Id)
            .Select(kv => kv.Key)
            .OrderBy(TerritoryScoring.IndexOf)
            .First();

        return new SelectHeadquartersCommand(Id, territory);
    }

    private (GameCommand, BotMemory) DecideReinforce(PlayerView view, BotMemory memory)
    {
        var seeded = SeedPool(view, memory);
        var pool = seeded.ReinforcePool ?? 0;

        if (pool <= 0)
        {
            return (new EndPhaseCommand(Id), seeded);
        }

        var target = view.Territories
            .Where(kv => kv.Value.Owner == Id)
            .Select(kv => kv.Key)
            .OrderBy(TerritoryScoring.IndexOf)
            .First();

        return (new PlaceTroopsCommand(Id, target, pool), seeded);
    }

    private BotMemory SeedPool(PlayerView view, BotMemory memory)
    {
        if (memory.ReinforcePoolSeeded)
        {
            return memory;
        }

        var baseAllotment = Reinforcement.Calculate(view.Territories, Id);
        var pool = (memory.ReinforcePool ?? 0) + baseAllotment;

        return memory with { ReinforcePool = pool, ReinforcePoolSeeded = true };
    }
}
