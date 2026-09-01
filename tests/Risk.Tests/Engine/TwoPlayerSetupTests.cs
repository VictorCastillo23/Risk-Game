using Risk.Domain.Players;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Setup;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

/// <summary>
/// PR1 of roadmap item 4.1: <see cref="GameMode.TwoPlayer"/>'s 3-way
/// territory deal (P1 / P2 / Neutral) via <c>TwoPlayerSetupStrategy</c>,
/// wired through <see cref="GameSetup.Create"/>. Setup completion (Phase A
/// budget generalization, Phase B neutral placement) is PR2/PR3's scope —
/// these tests only cover the initial dealt <see cref="GameState"/>.
/// </summary>
public class TwoPlayerSetupTests
{
    [Fact]
    public void Create_deals_42_territories_into_three_14_piles_with_one_neutral()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(2, GameMode.TwoPlayer, QueuedDiceRoller.ForRollOff(2)));
        var state = ok.State;

        Assert.Equal(3, state.Players.Count);

        var neutrals = state.Players.Where(p => p.IsNeutral).ToArray();
        Assert.Single(neutrals);
        Assert.Equal(new PlayerId(2), neutrals[0].Id);
        Assert.False(state.Players[0].IsNeutral);
        Assert.False(state.Players[1].IsNeutral);

        var counts = state.Territories.Values
            .GroupBy(t => t.Owner!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(3, counts.Count);
        Assert.Equal(42, counts.Values.Sum());
        Assert.All(counts.Values, count => Assert.Equal(14, count));
        Assert.All(state.Territories.Values, t => Assert.Equal(1, t.Troops));

        Assert.All(state.Players, p => Assert.Equal(26, p.TroopsRemaining));

        Assert.Equal(TurnPhase.Setup, state.Turn.Phase);
        Assert.Equal(state.Players[0].Id, state.Turn.CurrentPlayer);
    }

    [Fact]
    public void Create_assigns_120_total_troops_for_TwoPlayer_via_three_way_deal()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(2, GameMode.TwoPlayer, QueuedDiceRoller.ForRollOff(2)));
        var state = ok.State;

        var totalRemaining = state.Players.Sum(p => p.TroopsRemaining);
        var territoriesPlaced = state.Territories.Count; // 1 troop auto-placed per dealt territory

        Assert.Equal(3 * 40, totalRemaining + territoriesPlaced);
    }
}
