namespace Risk.Engine.State;

/// <summary>
/// The phases a turn cycles through, in order. Claim (unclaimed-territory
/// pick, not yet wired to any setup flow) precedes Setup's initial troop
/// placement, which happens once before any player's first real turn and is
/// followed by Reinforce for player 0.
/// </summary>
public enum TurnPhase
{
    Claim,
    Setup,
    Reinforce,
    Attack,
    Fortify
}
