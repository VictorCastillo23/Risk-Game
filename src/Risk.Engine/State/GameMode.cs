namespace Risk.Engine.State;

/// <summary>
/// The four Risk variants this engine supports, each with its own legal
/// player-count range (enforced by <see cref="Setup.GameSetup.Create"/>) and,
/// in later roadmap items, its own setup strategy, victory rule, and turn
/// order.
/// </summary>
/// <remarks>
/// Declaration order is NOT a serialization contract: nothing in this
/// solution serializes <see cref="GameMode"/> (no JSON, no binary format, no
/// database — <c>GameSessionService</c> holds <c>GameState</c> in memory only,
/// scoped per Blazor Server circuit). <see cref="Classic"/> is declared first
/// deliberately so <c>default(GameMode)</c> equals <see cref="Classic"/>,
/// matching the compat default already used by <c>GameState.Mode</c> and
/// <c>GameSessionService.Start</c>. Reordering the other three members is
/// free; adding a fifth member is the only change that needs review of every
/// exhaustive switch over this enum.
/// </remarks>
public enum GameMode
{
    Classic,
    SecretMission,
    TwoPlayer,
    Capital
}
