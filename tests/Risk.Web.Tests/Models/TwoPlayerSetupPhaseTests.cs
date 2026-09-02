using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.State;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

/// <summary>
/// Truth table for <see cref="TwoPlayerSetupPhase.IsPhaseB"/> (design D1/D4),
/// the Web-side mirror of <c>GameEngine.IsPhaseB</c>
/// (<c>src/Risk.Engine/GameEngine.cs:200-204</c>). Every case here is also
/// exercised end-to-end against the real engine by
/// <c>Risk.Web.Tests.Services.TwoPlayerSetupPhaseParityTests</c> — this file
/// only isolates the pure derived-state logic.
/// </summary>
public class TwoPlayerSetupPhaseTests
{
    private static readonly PlayerId Human0 = new(0);
    private static readonly PlayerId Human1 = new(1);
    private static readonly PlayerId Neutral = new(2);

    private static GameState BuildState(GameMode mode, TurnPhase phase, int human0Remaining, int human1Remaining, int? neutralRemaining)
    {
        var players = new List<PlayerState>
        {
            new(Human0, [], false, human0Remaining),
            new(Human1, [], false, human1Remaining)
        };

        if (neutralRemaining is { } remaining)
        {
            players.Add(new PlayerState(Neutral, [], false, remaining, IsNeutral: true));
        }

        return new GameState(
            new Dictionary<TerritoryId, TerritoryState>(),
            players,
            new TurnState(Human0, phase),
            [],
            [],
            new GameStatus.InProgress(),
            Mode: mode);
    }

    [Fact]
    public void Classic_WithNoNeutralPlayer_IsNotPhaseB_AndDoesNotThrow()
    {
        var state = BuildState(GameMode.Classic, TurnPhase.Setup, human0Remaining: 3, human1Remaining: 3, neutralRemaining: null);

        var result = TwoPlayerSetupPhase.IsPhaseB(state);

        Assert.False(result);
    }

    [Fact]
    public void TwoPlayer_MidPhaseA_WithHumansStillHoldingTroops_IsNotPhaseB()
    {
        var state = BuildState(GameMode.TwoPlayer, TurnPhase.Setup, human0Remaining: 1, human1Remaining: 0, neutralRemaining: 5);

        var result = TwoPlayerSetupPhase.IsPhaseB(state);

        Assert.False(result);
    }

    [Fact]
    public void TwoPlayer_BothHumansExhausted_WithNeutralTroopsLeft_IsPhaseB()
    {
        var state = BuildState(GameMode.TwoPlayer, TurnPhase.Setup, human0Remaining: 0, human1Remaining: 0, neutralRemaining: 5);

        var result = TwoPlayerSetupPhase.IsPhaseB(state);

        Assert.True(result);
    }

    [Fact]
    public void TwoPlayer_BothHumansAndNeutralExhausted_IsNotPhaseB()
    {
        var state = BuildState(GameMode.TwoPlayer, TurnPhase.Setup, human0Remaining: 0, human1Remaining: 0, neutralRemaining: 0);

        var result = TwoPlayerSetupPhase.IsPhaseB(state);

        Assert.False(result);
    }

    [Fact]
    public void TwoPlayer_InReinforcePhase_WithNeutralTroopsLeft_IsNotPhaseB()
    {
        var state = BuildState(GameMode.TwoPlayer, TurnPhase.Reinforce, human0Remaining: 0, human1Remaining: 0, neutralRemaining: 5);

        var result = TwoPlayerSetupPhase.IsPhaseB(state);

        Assert.False(result);
    }
}
