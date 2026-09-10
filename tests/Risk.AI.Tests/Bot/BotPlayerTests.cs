using Risk.AI.Decisions;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.State;

namespace Risk.AI.Tests.Bot;

/// <summary>
/// Tests <see cref="BotPlayer.DecideNextCommand"/>'s dispatch itself — does
/// it call the right <c>Decisions.*</c> method for a given phase/pending
/// state, and in the right precedence order (design's "byte-mirrors
/// GameEngine.Execute's validation order": pending occupation, then
/// mandatory card trade, then phase switch). Never re-asserts what a
/// specific <c>Decisions.*</c>/<c>Scoring.*</c> class itself decides — those
/// are covered by their own test files.
/// </summary>
public class BotPlayerTests
{
    private static readonly PlayerId Self = new(0);
    private static readonly PlayerId Enemy = new(1);

    private static readonly Card[] FiveOfAKindHand =
    [
        new TerritoryCard(new TerritoryId("Alaska"), CardSymbol.Infantry),
        new TerritoryCard(new TerritoryId("Alberta"), CardSymbol.Infantry),
        new TerritoryCard(new TerritoryId("Ontario"), CardSymbol.Infantry),
        new TerritoryCard(new TerritoryId("Quebec"), CardSymbol.Infantry),
        new TerritoryCard(new TerritoryId("Greenland"), CardSymbol.Infantry),
    ];

    // --- 7.1 dispatch precedence: occupation > mandatory trade > phase ---

    [Fact]
    public void DecideNextCommand_resolves_a_pending_occupation_before_a_mandatory_trade_or_the_phase()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 5, "Alaska")
            .Owns(Self, 0, "Alberta")
            .Owns(Enemy, 3, "NorthwestTerritory")
            .PendingOccupation("Alaska", "Alberta", 1)
            .Phase(TurnPhase.Attack)
            .MandatoryTradeDown()
            .Hand(FiveOfAKindHand)
            .Build();
        var bot = new BotPlayer(Self);

        var (command, _) = bot.DecideNextCommand(view, BotMemory.Empty);

        Assert.Equal(OccupyDecision.Decide(view, Self), command);
    }

    [Fact]
    public void DecideNextCommand_trades_cards_before_dispatching_by_phase_when_the_mandatory_flag_is_armed()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 5, "Alaska")
            .Owns(Enemy, 3, "NorthwestTerritory")
            .Phase(TurnPhase.Attack)
            .MandatoryTradeDown()
            .Hand(FiveOfAKindHand)
            .Build();
        var bot = new BotPlayer(Self);

        var (command, _) = bot.DecideNextCommand(view, BotMemory.Empty);

        Assert.IsType<TradeCardsCommand>(command);
    }

    [Fact]
    public void DecideNextCommand_trades_cards_at_reinforce_phase_start_even_without_the_mandatory_flag_armed()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 5, "Alaska")
            .Phase(TurnPhase.Reinforce)
            .Hand(FiveOfAKindHand)
            .Build();
        var bot = new BotPlayer(Self);

        var (command, _) = bot.DecideNextCommand(view, BotMemory.Empty);

        Assert.IsType<TradeCardsCommand>(command);
    }

    [Fact]
    public void DecideNextCommand_does_not_force_a_trade_below_the_five_card_threshold_outside_reinforce()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 5, "Alaska")
            .Owns(Enemy, 1, "NorthwestTerritory")
            .Phase(TurnPhase.Attack)
            .Hand(FiveOfAKindHand.Take(3).ToArray())
            .Build();
        var bot = new BotPlayer(Self);

        var (command, _) = bot.DecideNextCommand(view, BotMemory.Empty);

        Assert.IsNotType<TradeCardsCommand>(command);
    }

    // --- phase dispatch: each phase routes to its own Decisions.* method ---

    [Fact]
    public void DecideNextCommand_dispatches_claim_phase_to_ClaimDecision()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alberta")
            .Owns(Enemy, 1, "Ontario")
            .Unclaimed("Alaska")
            .Phase(TurnPhase.Claim)
            .Build();
        var bot = new BotPlayer(Self);
        var memoryIn = BotMemory.Empty;

        var (command, memory) = bot.DecideNextCommand(view, memoryIn);

        Assert.Equal(ClaimDecision.Decide(view, Self), command);
        Assert.Same(memoryIn, memory);
    }

    [Fact]
    public void DecideNextCommand_dispatches_setup_phase_to_SetupDecision()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 5, "Alberta")
            .Phase(TurnPhase.Setup)
            .Build();
        var bot = new BotPlayer(Self);
        var memoryIn = BotMemory.Empty;

        var (command, memory) = bot.DecideNextCommand(view, memoryIn);

        Assert.Equal(SetupDecision.Decide(view, Self, memoryIn), command);
        Assert.Same(memoryIn, memory);
    }

    [Fact]
    public void DecideNextCommand_dispatches_select_headquarters_phase_to_HeadquartersDecision()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska", "Alberta")
            .Phase(TurnPhase.SelectHeadquarters)
            .Build();
        var bot = new BotPlayer(Self);
        var memoryIn = BotMemory.Empty;

        var (command, memory) = bot.DecideNextCommand(view, memoryIn);

        Assert.Equal(HeadquartersDecision.Decide(view, Self), command);
        Assert.Same(memoryIn, memory);
    }

    [Fact]
    public void DecideNextCommand_dispatches_reinforce_phase_to_ReinforceDecision_and_carries_its_seeded_memory()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 1, "Alberta")
            .Phase(TurnPhase.Reinforce)
            .Build();
        var bot = new BotPlayer(Self);
        var memoryIn = BotMemory.Empty;

        var (command, memory) = bot.DecideNextCommand(view, memoryIn);

        var (expectedCommand, expectedMemory) = ReinforceDecision.Decide(view, Self, memoryIn);
        Assert.Equal(expectedCommand, command);
        Assert.Equal(expectedMemory, memory);
    }

    [Fact]
    public void DecideNextCommand_dispatches_attack_phase_to_AttackDecision()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 5, "Alaska")
            .Owns(Enemy, 1, "NorthwestTerritory")
            .Phase(TurnPhase.Attack)
            .Build();
        var bot = new BotPlayer(Self);
        var memoryIn = BotMemory.Empty;

        var (command, memory) = bot.DecideNextCommand(view, memoryIn);

        Assert.Equal(AttackDecision.Decide(view, Self, memoryIn), command);
        Assert.Same(memoryIn, memory);
    }

    [Fact]
    public void DecideNextCommand_dispatches_fortify_phase_to_FortifyDecision()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 5, "Alaska")
            .Owns(Self, 3, "Alberta")
            .Owns(Enemy, 9, "Ontario")
            .Phase(TurnPhase.Fortify)
            .Build();
        var bot = new BotPlayer(Self);
        var memoryIn = BotMemory.Empty;

        var (command, memory) = bot.DecideNextCommand(view, memoryIn);

        Assert.Equal(FortifyDecision.Decide(view, Self), command);
        Assert.Same(memoryIn, memory);
    }

    // --- 7.2 determinism ---

    [Fact]
    public void DecideNextCommand_is_deterministic_for_an_identical_view_and_memory_pair()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 5, "Alaska")
            .Owns(Enemy, 1, "NorthwestTerritory")
            .Phase(TurnPhase.Attack)
            .Build();
        var memory = BotMemory.Empty.WithSeenActor(Self).WithSeenActor(Enemy);
        var bot = new BotPlayer(Self);

        var first = bot.DecideNextCommand(view, memory);
        var second = bot.DecideNextCommand(view, memory);

        Assert.Equal(first.Command, second.Command);
        Assert.Equal(first.Memory, second.Memory);
    }

    [Fact]
    public void DecideNextCommand_is_deterministic_across_a_reinforce_phase_pool_seeding_pair()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 3, "Alaska")
            .Owns(Enemy, 1, "Alberta")
            .Phase(TurnPhase.Reinforce)
            .Build();
        var memory = BotMemory.Empty;
        var bot = new BotPlayer(Self);

        var first = bot.DecideNextCommand(view, memory);
        var second = bot.DecideNextCommand(view, memory);

        Assert.Equal(first.Command, second.Command);
        Assert.Equal(first.Memory, second.Memory);
    }

    [Fact]
    public void Id_returns_the_player_id_the_bot_was_constructed_with()
    {
        var bot = new BotPlayer(Self);

        Assert.Equal(Self, bot.Id);
    }
}
