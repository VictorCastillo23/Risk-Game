using Risk.AI.Tests.Fakes;
using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Events;
using Risk.Engine.State;

namespace Risk.AI.Tests.Bot;

public class BotMemoryTests
{
    private static readonly PlayerId Self = new(0);
    private static readonly PlayerId Other = new(1);

    private static readonly TerritoryCard AlaskaInfantry = new(new TerritoryId("Alaska"), CardSymbol.Infantry);
    private static readonly TerritoryCard AlbertaInfantry = new(new TerritoryId("Alberta"), CardSymbol.Infantry);
    private static readonly TerritoryCard OntarioInfantry = new(new TerritoryId("Ontario"), CardSymbol.Infantry);

    [Fact]
    public void Empty_has_no_pool_no_setup_troops_and_no_seen_actors()
    {
        var memory = BotMemory.Empty;

        Assert.Null(memory.ReinforcePool);
        Assert.Equal(0, memory.OwnSetupTroopsPlaced);
        Assert.Empty(memory.SeenActors);
    }

    [Fact]
    public void WithSeenActor_adds_the_actor_to_seen_actors()
    {
        var memory = BotMemory.Empty.WithSeenActor(Self);

        Assert.Contains(Self, memory.SeenActors);
    }

    [Fact]
    public void WithSeenActor_accumulates_distinct_actors_across_calls()
    {
        var memory = BotMemory.Empty.WithSeenActor(Self).WithSeenActor(Other);

        Assert.Equal(new HashSet<PlayerId> { Self, Other }, memory.SeenActors);
    }

    [Fact]
    public void WithSeenActor_is_idempotent_for_an_already_seen_actor()
    {
        var memory = BotMemory.Empty.WithSeenActor(Self);

        var again = memory.WithSeenActor(Self);

        Assert.Single(again.SeenActors);
    }

    [Fact]
    public void Fold_decrements_the_pool_when_the_bot_places_troops_during_its_own_reinforce_visit()
    {
        var memory = BotMemory.Empty with { ReinforcePool = 5 };
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Reinforce).Build();
        var events = new GameEvent[] { new TroopsPlaced(Self, new TerritoryId("Alaska"), 2) };

        var folded = BotMemory.Fold(memory, Self, view, events);

        Assert.Equal(3, folded.ReinforcePool);
    }

    [Fact]
    public void Fold_ignores_troops_placed_by_another_player_during_reinforce()
    {
        var memory = BotMemory.Empty with { ReinforcePool = 5 };
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Reinforce).Build();
        var events = new GameEvent[] { new TroopsPlaced(Other, new TerritoryId("Alaska"), 2) };

        var folded = BotMemory.Fold(memory, Self, view, events);

        Assert.Equal(5, folded.ReinforcePool);
    }

    [Fact]
    public void Fold_increments_setup_troops_placed_when_the_bot_places_troops_during_setup()
    {
        var memory = BotMemory.Empty;
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Setup).Build();
        var events = new GameEvent[] { new TroopsPlaced(Self, new TerritoryId("Alaska"), 1) };

        var folded = BotMemory.Fold(memory, Self, view, events);

        Assert.Equal(1, folded.OwnSetupTroopsPlaced);
    }

    [Fact]
    public void Fold_does_not_touch_the_pool_when_the_bot_places_troops_during_setup()
    {
        var memory = BotMemory.Empty with { ReinforcePool = 4 };
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Setup).Build();
        var events = new GameEvent[] { new TroopsPlaced(Self, new TerritoryId("Alaska"), 1) };

        var folded = BotMemory.Fold(memory, Self, view, events);

        Assert.Equal(4, folded.ReinforcePool);
    }

    [Fact]
    public void Fold_does_not_increment_setup_troops_placed_from_a_territory_claim()
    {
        var memory = BotMemory.Empty;
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Claim).Build();
        var events = new GameEvent[] { new TerritoryClaimed(Self, new TerritoryId("Alaska"), 1) };

        var folded = BotMemory.Fold(memory, Self, view, events);

        Assert.Equal(0, folded.OwnSetupTroopsPlaced);
    }

    [Fact]
    public void Fold_increments_the_pool_when_a_card_trade_happens_during_the_bots_own_reinforce_visit()
    {
        var memory = BotMemory.Empty with { ReinforcePool = 3 };
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Reinforce).Build();
        var events = new GameEvent[]
        {
            new CardsTraded(Self, new Card[] { AlaskaInfantry, AlbertaInfantry, OntarioInfantry }, 6),
        };

        var folded = BotMemory.Fold(memory, Self, view, events);

        Assert.Equal(9, folded.ReinforcePool);
    }

    [Fact]
    public void Fold_ignores_a_card_trade_bonus_during_an_attack_phase_mandatory_trade_down()
    {
        var memory = BotMemory.Empty with { ReinforcePool = 3 };
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Attack).Build();
        var events = new GameEvent[]
        {
            new CardsTraded(Self, new Card[] { AlaskaInfantry, AlbertaInfantry, OntarioInfantry }, 8),
        };

        var folded = BotMemory.Fold(memory, Self, view, events);

        Assert.Equal(3, folded.ReinforcePool);
    }

    [Fact]
    public void Fold_ignores_a_card_trade_by_another_player_during_reinforce()
    {
        var memory = BotMemory.Empty with { ReinforcePool = 3 };
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Reinforce).Build();
        var events = new GameEvent[]
        {
            new CardsTraded(Other, new Card[] { AlaskaInfantry, AlbertaInfantry, OntarioInfantry }, 6),
        };

        var folded = BotMemory.Fold(memory, Self, view, events);

        Assert.Equal(3, folded.ReinforcePool);
    }

    [Fact]
    public void Fold_adds_only_the_escalating_bonus_when_the_flat_territory_bonus_also_landed()
    {
        var memory = BotMemory.Empty with { ReinforcePool = 3 };
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Reinforce).Build();
        var events = new GameEvent[]
        {
            new CardsTraded(Self, new Card[] { AlaskaInfantry, AlbertaInfantry, OntarioInfantry }, 6, new TerritoryId("Alaska")),
        };

        var folded = BotMemory.Fold(memory, Self, view, events);

        Assert.Equal(9, folded.ReinforcePool);
    }

    [Fact]
    public void Fold_resets_the_pool_to_null_when_the_bots_own_reinforce_phase_ends()
    {
        var memory = BotMemory.Empty with { ReinforcePool = 2 };
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Reinforce).Build();
        var events = new GameEvent[] { new PhaseChanged(TurnPhase.Reinforce, TurnPhase.Attack, Self) };

        var folded = BotMemory.Fold(memory, Self, view, events);

        Assert.Null(folded.ReinforcePool);
    }

    [Fact]
    public void Fold_does_not_reset_the_pool_when_another_players_reinforce_phase_ends()
    {
        var memory = BotMemory.Empty with { ReinforcePool = 2 };
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Reinforce).Build();
        var events = new GameEvent[] { new PhaseChanged(TurnPhase.Reinforce, TurnPhase.Attack, Other) };

        var folded = BotMemory.Fold(memory, Self, view, events);

        Assert.Equal(2, folded.ReinforcePool);
    }

    [Fact]
    public void Fold_does_not_reset_the_pool_for_a_phase_change_that_is_not_reinforce_to_attack()
    {
        var memory = BotMemory.Empty with { ReinforcePool = 2 };
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Attack).Build();
        var events = new GameEvent[] { new PhaseChanged(TurnPhase.Attack, TurnPhase.Fortify, Self) };

        var folded = BotMemory.Fold(memory, Self, view, events);

        Assert.Equal(2, folded.ReinforcePool);
    }

    [Fact]
    public void Fold_leaves_memory_unchanged_for_events_it_does_not_track()
    {
        var memory = BotMemory.Empty with { ReinforcePool = 5, OwnSetupTroopsPlaced = 1 };
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Attack).Build();
        var events = new GameEvent[] { new TerritoryConquered(Self, Other, new TerritoryId("Alaska")) };

        var folded = BotMemory.Fold(memory, Self, view, events);

        Assert.Equal(memory, folded);
    }

    [Fact]
    public void Fold_does_not_modify_seen_actors()
    {
        var memory = BotMemory.Empty.WithSeenActor(Self).WithSeenActor(Other) with { ReinforcePool = 5 };
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Reinforce).Build();
        var events = new GameEvent[] { new TroopsPlaced(Self, new TerritoryId("Alaska"), 1) };

        var folded = BotMemory.Fold(memory, Self, view, events);

        Assert.Equal(memory.SeenActors, folded.SeenActors);
    }

    [Fact]
    public void Fold_accumulates_across_a_trade_followed_by_a_placement_in_the_same_reinforce_visit()
    {
        var memory = BotMemory.Empty with { ReinforcePool = 3 };
        var view = PlayerViewBuilder.For(Self).Phase(TurnPhase.Reinforce).Build();

        var afterTrade = BotMemory.Fold(
            memory,
            Self,
            view,
            new GameEvent[] { new CardsTraded(Self, new Card[] { AlaskaInfantry, AlbertaInfantry, OntarioInfantry }, 6) });

        var afterPlacement = BotMemory.Fold(
            afterTrade,
            Self,
            view,
            new GameEvent[] { new TroopsPlaced(Self, new TerritoryId("Alaska"), 9) });

        Assert.Equal(0, afterPlacement.ReinforcePool);
    }
}
