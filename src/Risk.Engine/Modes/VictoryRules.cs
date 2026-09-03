using Risk.Engine.State;

namespace Risk.Engine.Modes;

/// <summary>
/// Resolves the <see cref="IVictoryRule"/> a <see cref="GameMode"/> should be
/// checked through. Every <see cref="GameMode"/> now resolves to a real rule
/// (roadmap item 5.3 gave <see cref="GameMode.Capital"/> its own
/// <see cref="CapitalVictoryRule"/>) — the nullable return type and
/// <c>GameEngine.ExecuteAttack</c>'s pre-refactor inline "actor owns every
/// territory" fallback remain only as the test-injection seam for
/// <c>victoryRuleFor</c>. Deliberately has no <c>_</c> discard arm — an
/// unhandled <see cref="GameMode"/> value throws
/// <see cref="SwitchExpressionException"/> instead of silently defaulting,
/// and adding a 5th mode without updating this switch produces a CS8524
/// exhaustiveness warning at compile time, matching
/// <c>Setup.GameSetup.PlayerCountRange</c>'s convention.
/// </summary>
public static class VictoryRules
{
    private static readonly IVictoryRule ConquestVictory = new ConquestVictoryRule();
    private static readonly IVictoryRule TwoPlayerVictory = new TwoPlayerVictoryRule();
    private static readonly IVictoryRule SecretMissionVictory = new SecretMissionVictoryRule();
    private static readonly IVictoryRule CapitalVictory = new CapitalVictoryRule();

    public static IVictoryRule? For(GameMode mode) => mode switch
    {
        GameMode.Classic => ConquestVictory,
        GameMode.SecretMission => SecretMissionVictory,
        GameMode.TwoPlayer => TwoPlayerVictory,
        GameMode.Capital => CapitalVictory
    };
}
