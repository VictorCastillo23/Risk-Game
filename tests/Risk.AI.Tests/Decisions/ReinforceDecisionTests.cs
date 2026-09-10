using Risk.AI.Decisions;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Rules;
using Risk.Engine.State;

namespace Risk.AI.Tests.Decisions;

public class ReinforceDecisionTests
{
    private static readonly PlayerId Self = new(0);
    private static readonly PlayerId Enemy = new(1);

    private static readonly TerritoryCard AlaskaInfantry = new(new TerritoryId("Alaska"), CardSymbol.Infantry);
    private static readonly TerritoryCard AlbertaInfantry = new(new TerritoryId("Alberta"), CardSymbol.Infantry);
    private static readonly TerritoryCard OntarioInfantry = new(new TerritoryId("Ontario"), CardSymbol.Infantry);

    [Fact]
    public void Decide_seeds_the_pool_from_Reinforcement_Calculate_and_places_the_whole_pool()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Phase(TurnPhase.Reinforce)
            .Build();

        var expectedPool = Reinforcement.Calculate(view.Territories, Self);

        var (command, memory) = ReinforceDecision.Decide(view, Self, BotMemory.Empty);

        var placement = Assert.IsType<PlaceTroopsCommand>(command);
        Assert.Equal(new TerritoryId("Alaska"), placement.Territory);
        Assert.Equal(expectedPool, placement.Troops);
        Assert.True(memory.ReinforcePoolSeeded);
        Assert.Equal(expectedPool, memory.ReinforcePool);
    }

    [Fact]
    public void Decide_trades_a_valid_card_set_before_placing_any_troops()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Hand(AlaskaInfantry, AlbertaInfantry, OntarioInfantry)
            .Phase(TurnPhase.Reinforce)
            .Build();

        var (command, memory) = ReinforceDecision.Decide(view, Self, BotMemory.Empty);

        var trade = Assert.IsType<TradeCardsCommand>(command);
        Assert.True(CardSet.IsValid(trade.Cards));
        Assert.True(memory.ReinforcePoolSeeded);
    }

    [Fact]
    public void Decide_ends_the_phase_only_when_the_tracked_pool_is_exactly_zero()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Phase(TurnPhase.Reinforce)
            .Build();

        var memory = BotMemory.Empty with { ReinforcePool = 0, ReinforcePoolSeeded = true };

        var (command, resultMemory) = ReinforceDecision.Decide(view, Self, memory);

        Assert.IsType<EndPhaseCommand>(command);
        Assert.Equal(0, resultMemory.ReinforcePool);
    }

    [Fact]
    public void Decide_adds_the_base_allotment_to_an_unseeded_pool_that_already_holds_an_early_banked_bonus()
    {
        // Regression for the Phase 4 seeding contract: a mandatory trade-at-turn-start can
        // bank a CardsTraded bonus into ReinforcePool via Fold BEFORE ReinforceDecision ever
        // runs this visit (ReinforcePoolSeeded stays false). Seeding must ADD the base
        // allotment to that pre-banked value, never overwrite/coalesce it away.
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Phase(TurnPhase.Reinforce)
            .Build();

        var baseAllotment = Reinforcement.Calculate(view.Territories, Self);
        var memory = BotMemory.Empty with { ReinforcePool = 6, ReinforcePoolSeeded = false };

        var (command, resultMemory) = ReinforceDecision.Decide(view, Self, memory);

        Assert.Equal(6 + baseAllotment, resultMemory.ReinforcePool);
        Assert.True(resultMemory.ReinforcePoolSeeded);
        var placement = Assert.IsType<PlaceTroopsCommand>(command);
        Assert.Equal(6 + baseAllotment, placement.Troops);
    }

    [Fact]
    public void Decide_does_not_reseed_the_base_allotment_once_already_seeded()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Phase(TurnPhase.Reinforce)
            .Build();

        // Already seeded and partially spent this visit; must NOT add Reinforcement.Calculate again.
        var memory = BotMemory.Empty with { ReinforcePool = 2, ReinforcePoolSeeded = true };

        var (command, resultMemory) = ReinforceDecision.Decide(view, Self, memory);

        Assert.Equal(2, resultMemory.ReinforcePool);
        var placement = Assert.IsType<PlaceTroopsCommand>(command);
        Assert.Equal(2, placement.Troops);
    }

    [Fact]
    public void Decide_tops_up_the_exact_amount_needed_when_the_mission_wants_a_top_up()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 1, "Alaska")
            .Owns(Self, 5, "Alberta")
            .Mission(new OccupyTerritories(18, MinArmiesPerTerritory: 2))
            .Phase(TurnPhase.Reinforce)
            .Build();

        var memory = BotMemory.Empty with { ReinforcePool = 5, ReinforcePoolSeeded = true };

        var (command, _) = ReinforceDecision.Decide(view, Self, memory);

        var placement = Assert.IsType<PlaceTroopsCommand>(command);
        Assert.Equal(new TerritoryId("Alaska"), placement.Territory);
        Assert.Equal(1, placement.Troops); // 2 (threshold) - 1 (current) = 1, capped by pool of 5.
    }

    [Fact]
    public void Decide_falls_back_to_the_full_pool_on_the_best_territory_once_every_mission_top_up_is_satisfied()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 5, "Alaska")
            .Mission(new OccupyTerritories(18, MinArmiesPerTerritory: 2))
            .Phase(TurnPhase.Reinforce)
            .Build();

        var memory = BotMemory.Empty with { ReinforcePool = 4, ReinforcePoolSeeded = true };

        var (command, _) = ReinforceDecision.Decide(view, Self, memory);

        var placement = Assert.IsType<PlaceTroopsCommand>(command);
        Assert.Equal(new TerritoryId("Alaska"), placement.Territory);
        Assert.Equal(4, placement.Troops);
    }

    [Fact]
    public void Decide_places_the_full_pool_on_the_most_urgent_frontier_territory_over_a_safe_interior_one()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Self, 3, "Peru")
            .Owns(Enemy, 6, "Alberta")
            .Phase(TurnPhase.Reinforce)
            .Build();

        var memory = BotMemory.Empty with { ReinforcePool = 4, ReinforcePoolSeeded = true };

        var (command, _) = ReinforceDecision.Decide(view, Self, memory);

        // Alaska borders Alberta (enemy, 6 troops): urgency 3. Peru has no enemy neighbor: urgency 0.
        var placement = Assert.IsType<PlaceTroopsCommand>(command);
        Assert.Equal(new TerritoryId("Alaska"), placement.Territory);
        Assert.Equal(4, placement.Troops);
    }
}
