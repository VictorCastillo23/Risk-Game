using Risk.Engine.State;

namespace Risk.Engine.Modes;

/// <summary>
/// Resolves the <see cref="IVictoryRule"/> a <see cref="GameMode"/> should be
/// checked through, or <see langword="null"/> when that mode still relies on
/// <c>GameEngine.ExecuteAttack</c>'s pre-refactor inline "actor owns every
/// territory" check. Only <see cref="GameMode.Capital"/> still returns
/// <see langword="null"/> — its real rule is roadmap item 5.3. Deliberately
/// has no <c>_</c> discard arm — an unhandled <see cref="GameMode"/> value
/// throws <see cref="SwitchExpressionException"/> instead of silently
/// defaulting, and adding a 5th mode without updating this switch produces a
/// CS8524 exhaustiveness warning at compile time, matching
/// <c>Setup.GameSetup.PlayerCountRange</c>'s convention.
/// </summary>
public static class VictoryRules
{
    private static readonly IVictoryRule ConquestVictory = new ConquestVictoryRule();
    private static readonly IVictoryRule TwoPlayerVictory = new TwoPlayerVictoryRule();
    private static readonly IVictoryRule SecretMissionVictory = new SecretMissionVictoryRule();

    public static IVictoryRule? For(GameMode mode) => mode switch
    {
        GameMode.Classic => ConquestVictory,
        GameMode.SecretMission => SecretMissionVictory,
        GameMode.TwoPlayer => TwoPlayerVictory,
        GameMode.Capital => null
    };
}
