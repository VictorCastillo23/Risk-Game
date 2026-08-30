using Risk.Domain.Cards;
using Risk.Domain.Errors;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

/// <summary>
/// <see cref="ClaimTerritoryCommand"/> is unreachable via any built-in flow
/// in this item (roadmap 1.3): nothing constructs a <see cref="TurnPhase.Claim"/>
/// state yet (item 2.1 wires it). These tests hand-build minimal
/// <see cref="TurnPhase.Claim"/> states and call <see cref="GameEngine.Execute"/>
/// directly — the exact contract 2.1 will wire.
/// </summary>
public class ClaimTerritoryCommandTests
{
    private static readonly TerritoryId Alaska = new("Alaska"); // unclaimed in the test fixture
    private static readonly TerritoryId NorthwestTerritory = new("NorthwestTerritory"); // already owned by "other"

    [Fact]
    public void Execute_claims_an_unowned_territory_and_decrements_the_actors_troop_pool()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseState(actor, other, actorTroopsRemaining: 5);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, Alaska, 1));

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        var claimed = ok.State.Territories[Alaska];
        Assert.Equal(actor, claimed.Owner);
        Assert.Equal(1, claimed.Troops);
        Assert.Equal(4, ok.State.Players.Single(p => p.Id == actor).TroopsRemaining);

        var claimedEvent = Assert.IsType<TerritoryClaimed>(Assert.Single(ok.Events));
        Assert.Equal(actor, claimedEvent.Player);
        Assert.Equal(Alaska, claimedEvent.Territory);
        Assert.Equal(1, claimedEvent.Troops);

        // D4: claiming never advances the turn or phase.
        Assert.Equal(TurnPhase.Claim, ok.State.Turn.Phase);
        Assert.Equal(actor, ok.State.Turn.CurrentPlayer);
    }

    [Fact]
    public void Execute_rejects_claiming_an_already_owned_territory()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseState(actor, other, actorTroopsRemaining: 5);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, NorthwestTerritory, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotOwner, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_an_unknown_territory()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseState(actor, other, actorTroopsRemaining: 5);
        var engine = new GameEngine(new QueuedDiceRoller());
        var unknown = new TerritoryId("NotOnTheMap");

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, unknown, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotOwner, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_a_troop_count_below_one()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseState(actor, other, actorTroopsRemaining: 5);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, Alaska, 0));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidTroopCount, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_a_troop_count_above_the_actors_remaining_pool()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseState(actor, other, actorTroopsRemaining: 2);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, Alaska, 3));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidTroopCount, rejection.Error.Code);
    }

    [Theory]
    [InlineData(TurnPhase.Setup)]
    [InlineData(TurnPhase.Reinforce)]
    [InlineData(TurnPhase.Attack)]
    [InlineData(TurnPhase.Fortify)]
    public void Execute_rejects_claiming_in_every_non_claim_phase(TurnPhase phase)
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseState(actor, other, actorTroopsRemaining: 5) with
        {
            Turn = new TurnState(actor, phase)
        };
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(actor, Alaska, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.WrongPhase, rejection.Error.Code);
    }

    [Fact]
    public void Execute_rejects_a_claim_from_a_player_who_is_not_the_current_player()
    {
        var actor = new PlayerId(0);
        var other = new PlayerId(1);
        var state = BuildClaimPhaseState(actor, other, actorTroopsRemaining: 5);
        var engine = new GameEngine(new QueuedDiceRoller());

        var result = engine.Execute(state, new ClaimTerritoryCommand(other, Alaska, 1));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.NotYourTurn, rejection.Error.Code);
    }

    /// <summary>
    /// Minimal hand-built <see cref="TurnPhase.Claim"/> state: <see cref="Alaska"/>
    /// is unclaimed (<c>Owner == null</c>), <see cref="NorthwestTerritory"/> is
    /// already owned by <paramref name="other"/>. No built-in flow produces this
    /// shape yet — see the class doc.
    /// </summary>
    private static GameState BuildClaimPhaseState(PlayerId currentPlayer, PlayerId other, int actorTroopsRemaining)
    {
        var territories = new Dictionary<TerritoryId, TerritoryState>
        {
            [Alaska] = new TerritoryState(null, 0),
            [NorthwestTerritory] = new TerritoryState(other, 1)
        };

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(currentPlayer, [], false, actorTroopsRemaining),
            new PlayerState(other, [], false, 0)
        ];

        var turn = new TurnState(currentPlayer, TurnPhase.Claim);

        return new GameState(territories, players, turn, Deck.CreateStandard(), [], new GameStatus.InProgress());
    }
}
