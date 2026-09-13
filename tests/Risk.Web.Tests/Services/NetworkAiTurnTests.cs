using Risk.AI;
using Risk.Domain.Dice;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.State;
using Risk.Web.Models;
using Risk.Web.Services;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Services;

/// <summary>
/// AI seats in networked games: a real <see cref="BotPlayer"/> drains bot
/// turns after each human dispatch; an illegal bot command hard-fails into
/// <c>AiFailure</c> with state untouched and no retry (the Risk.AI
/// zero-rejected invariant, surfaced rather than masked).
/// </summary>
public class NetworkAiTurnTests
{
    private static NetworkGameSession NewThreeSeat(
        IGameEngine engine,
        IDiceRoller dice,
        Func<PlayerId, IBotPlayer>? botFactory = null)
    {
        var session = botFactory is null
            ? new NetworkGameSession(engine, dice, new JoinCode("R7K9X2"))
            : new NetworkGameSession(engine, dice, new JoinCode("R7K9X2"), botFactory);
        session.ClaimSeat("Ana", "#FF0000", "c0");
        session.ClaimSeat("Bot", "#00FF00", "c1", isAi: true);
        session.ClaimSeat("Ceci", "#1D4ED8", "c2");
        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(session.Start(GameMode.Classic));
        Assert.Equal(new PlayerId(0), session.State!.Turn.CurrentPlayer);
        return session;
    }

    [Fact]
    public void HumanClaim_DrainsBotSeat_WithRealBot()
    {
        var session = NewThreeSeat(
            new GameEngine(new AlwaysAttackerWinsDiceRoller()),
            QueuedDiceRoller.ForRollOff(3));
        var humanTerritory = WorldMap.Territories[0].Id;

        var result = session.Dispatch(new ClaimTerritoryCommand(new PlayerId(0), humanTerritory, 1));

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Null(session.AiFailure);
        // The bot claimed exactly one territory on its drained turn, and
        // play now waits on the next human — no device has to poke the bot.
        var botTerritories = session.State!.Territories
            .Where(t => t.Value.Owner == new PlayerId(1))
            .ToList();
        Assert.Single(botTerritories);
        Assert.Equal(new PlayerId(2), session.State.Turn.CurrentPlayer);
    }

    [Fact]
    public void BotRejected_SetsAiFailure_LeavesStateUnchanged_AndNeverRetries()
    {
        var session = NewThreeSeat(
            new GameEngine(new AlwaysAttackerWinsDiceRoller()),
            QueuedDiceRoller.ForRollOff(3),
            id => new RejectingBotStub(id));
        var humanTerritory = WorldMap.Territories[0].Id;

        var result = session.Dispatch(new ClaimTerritoryCommand(new PlayerId(0), humanTerritory, 1));

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.NotNull(session.AiFailure);
        var failure = session.AiFailure!;
        Assert.Equal(new PlayerId(1), failure.Player);
        Assert.NotNull(failure.Error);
        // Human claim landed; the bot owns nothing and the turn never
        // advanced past it — one hard stop, zero retries.
        Assert.Equal(new PlayerId(0), session.State!.Territories[humanTerritory].Owner);
        Assert.DoesNotContain(session.State.Territories, t => t.Value.Owner == new PlayerId(1));
        Assert.Equal(new PlayerId(1), session.State.Turn.CurrentPlayer);
    }

    [Fact]
    public void Restore_RebuildsBotRegistry()
    {
        var source = NewThreeSeat(
            new GameEngine(new AlwaysAttackerWinsDiceRoller()),
            QueuedDiceRoller.ForRollOff(3));
        var started = source.State!;

        var session = new NetworkGameSession(
            new GameEngine(new AlwaysAttackerWinsDiceRoller()),
            QueuedDiceRoller.ForRollOff(1),
            new JoinCode("Q2W8Z4"));
        session.Restore(
            started,
            [new PlayerConfig(new PlayerId(0), "Ana", "#FF0000", false),
             new PlayerConfig(new PlayerId(1), "Bot", "#00FF00", true),
             new PlayerConfig(new PlayerId(2), "Ceci", "#1D4ED8", false)]);

        var result = session.Dispatch(new ClaimTerritoryCommand(new PlayerId(0), WorldMap.Territories[0].Id, 1));

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
        Assert.Null(session.AiFailure);
        Assert.Single(session.State!.Territories.Where(t => t.Value.Owner == new PlayerId(1)));
    }
}
